import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

base=Path('D:/projects/Pirate_BR/PirateGame')
out=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((out/'BilgeDeck.json').read_text(encoding='utf-8'))
root=bpy.data.objects['Ship_V3_Fitted_Root']
assert not root.get('bilge_supports_version'), 'Hold supports already authored'
document=json.loads((out.parent/'ShipV3.json').read_text(encoding='utf-8'))
vertices=[]
faces=[]
for piece in document['pieces']:
    if piece['family']!='Hull':continue
    obj=bpy.data.objects[piece['intact']]
    start=len(vertices)
    vertices.extend(obj.matrix_world@v.co for v in obj.data.vertices)
    faces.extend(tuple(start+i for i in p.vertices) for p in obj.data.polygons)
hull=BVHTree.FromPolygons(vertices,faces)
collection=bpy.data.collections.new('V20_Hold_Supports')
bpy.context.scene.collection.children.link(collection)
material=bpy.data.materials['V4_Hull_Ship_Art_Hull_Honey_Oak_V0']
new=[]
floor=manifest['floor_z']
ceiling=manifest['rail_ceiling_z']
width=.3

def object_for(name,mesh=None,parent=root,matrix=None):
    obj=bpy.data.objects.new(name,mesh)
    collection.objects.link(obj)
    obj.parent=parent
    obj.matrix_parent_inverse=Matrix.Identity(4)
    obj.matrix_world=matrix if matrix is not None else Matrix.Identity(4)
    obj['bilge_destructible_support']=True
    obj['exclude_from_export']=True
    new.append(name)
    return obj

def timber_mesh(name,height):
    bm=bmesh.new()
    bmesh.ops.create_cube(bm,size=1)
    for v in bm.verts:v.co=Vector((v.co.x*width,v.co.y*width,v.co.z*height))
    bmesh.ops.bevel(bm,geom=list(bm.edges),offset=.008,segments=1,affect='EDGES')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    uv=bm.loops.layers.uv.new('UVMap')
    for face in bm.faces:
        axis=max(range(3),key=lambda i:abs(face.normal[i]))
        for loop in face.loops:
            v=loop.vert.co
            loop[uv].uv=((v.z+height*.5)*.28,v.y*.6+.35) if axis==0 else ((v.z+height*.5)*.28,v.x*.6+.35) if axis==1 else (v.x*.6+.35,v.y*.6+.35)
    mesh=bpy.data.meshes.new(name)
    bm.to_mesh(mesh);bm.free()
    mesh.materials.append(material)
    return mesh

def clip(mesh,co,no,above):
    bm=bmesh.new();bm.from_mesh(mesh)
    cut=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=co,plane_no=no,clear_inner=above,clear_outer=not above)
    edges=[e for e in cut['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_valid and e.is_boundary]
    if edges:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    result=mesh.copy();bm.to_mesh(result);bm.free();return result

supports=[]
for end,y in [('Bow',11.4),('Stern',-11.4)]:
    for side,sign in [('Port',-1),('Starboard',1)]:
        distances=[]
        for z in [floor+.05,floor+.5,floor+1.5,ceiling-.05]:
            p,n,index,d=hull.ray_cast(Vector((0,y,z)),Vector((sign,0,0)),15)
            assert p is not None, (end,side,z)
            distances.append(abs(p.x))
        x=sign*(min(distances)-.36)
        key='BilgeBeam_'+end+'_'+side
        middle=(floor+ceiling)*.5
        matrix=Matrix.Translation((x,y,middle))
        group=object_for(key)
        intact=object_for(key+'_Intact',timber_mesh(key,ceiling-floor),group,matrix)
        fragments=[]
        for band in range(4):
            lo=-(ceiling-floor)*.5+band*(ceiling-floor)/4
            hi=lo+(ceiling-floor)/4
            for half in range(2):
                a=clip(intact.data,(0,0,lo),(0,0,1),True)
                b=clip(a,(0,0,hi),(0,0,1),False);bpy.data.meshes.remove(a)
                c=clip(b,(0,0,0),(1,.25,0),half==1);bpy.data.meshes.remove(b)
                name=key+'_Fragment_'+str(len(fragments)).zfill(2)
                obj=object_for(name,c,group,matrix)
                obj.hide_render=True;obj.hide_set(True)
                fragments.append(name)
        lamp='BilgeLamp_'+end+'_'+side
        mountpoint=Vector((x-sign*.5,y,floor+2.83))
        old=bpy.data.objects['V3_Lamp_Bow_Port_Mount'].matrix_world.translation
        rotation=Matrix.Rotation(math.pi if sign<0 else 0,4,'Z')
        delta=Matrix.Translation(mountpoint)@rotation@Matrix.Translation(-old)
        copies={}
        for suffix in ['Mount','Pivot','Body','LightSocket','Static_Hanger','Bracket']:
            source=bpy.data.objects['V3_Lamp_Bow_Port_'+suffix]
            data=source.data.copy() if source.type=='MESH' else None
            obj=object_for(lamp+'_'+suffix,data,root,delta@source.matrix_world)
            copies[suffix]=obj
        for suffix,parent in [('Pivot','Mount'),('Body','Pivot'),('LightSocket','Pivot'),('Static_Hanger','Mount'),('Bracket','Mount')]:
            obj=copies[suffix];matrix=obj.matrix_world.copy();obj.parent=copies[parent];obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_world=matrix
        supports.append({'name':key,'intact':intact.name,'fragments':fragments,'lamp':lamp,'position':[x,y,floor]})

manifest['supports']=supports
manifest['new_objects'].extend(new)
manifest['pump_down_angle']=82
manifest['version']=max(12,manifest['version'])
root['bilge_supports_version']=1
root['bilge_deck_version']=max(12,root.get('bilge_deck_version',0))
root['bilge_pump_down_angle']=82
(out/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.view_layer.update()
result={'supports':supports,'new_objects':len(new),'pump_down_angle':82}
