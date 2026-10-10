import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

BASE=Path('D:/projects/Pirate_BR/PirateGame')
OUT=BASE/'Assets/Models/Ships/ShipV3/Bilge'
OUT.mkdir(parents=True,exist_ok=True)
root=bpy.data.objects['Ship_V3_Fitted_Root']
assert not root.get('bilge_deck_version'), 'Bilge deck already added'
document=json.loads((OUT.parent/'ShipV3.json').read_text(encoding='utf-8'))
pieces=document['pieces']
floor_z=5.21
hole=(-.75,.75,3.45,10.00)
collection=bpy.data.collections.new('V19_Bilge_Deck')
bpy.context.scene.collection.children.link(collection)
new_objects=[]
modified=set()
empty=set()
protected=[]
masks={}
anchors=[]

def bounds(o):
    points=[o.matrix_world@Vector(v) for v in o.bound_box]
    return [min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]

def fresh(o,name,parent=root):
    obj=o.copy()
    obj.data=o.data.copy()
    obj.animation_data_clear()
    obj.name=name
    for key in list(obj.keys()):del obj[key]
    collection.objects.link(obj)
    obj.parent=parent
    obj.matrix_parent_inverse=Matrix.Identity(4)
    obj.matrix_world=o.matrix_world.copy()
    obj.hide_viewport=obj.hide_render=obj.hide_select=False
    obj.hide_set(False)
    obj['bilge_permanent']=True
    obj['exclude_from_export']=True
    new_objects.append(obj)
    return obj

def half(mesh,coordinate,axis,keep_above):
    bm=bmesh.new()
    bm.from_mesh(mesh)
    co=[0,0,0]
    no=[0,0,0]
    co[axis]=coordinate
    no[axis]=1
    result=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=co,plane_no=no,clear_inner=keep_above,clear_outer=not keep_above)
    edges=[e for e in result['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_valid and e.is_boundary]
    if edges:
        bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    if bm.faces:bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    output=mesh.copy()
    bm.to_mesh(output)
    bm.free()
    output.update()
    return output

def world_mesh(o):
    mesh=o.data.copy()
    mesh.transform(o.matrix_world)
    return mesh

def opening(mesh):
    slabs=[]
    slabs.append(half(mesh,hole[2],1,False))
    slabs.append(half(mesh,hole[3],1,True))
    middle=half(mesh,hole[2],1,True)
    center=half(middle,hole[3],1,False)
    slabs.append(half(center,hole[0],0,False))
    slabs.append(half(center,hole[1],0,True))
    bm=bmesh.new()
    for part in slabs:bm.from_mesh(part)
    output=mesh.copy()
    bm.to_mesh(output)
    bm.free()
    for part in slabs+[middle,center]:bpy.data.meshes.remove(part)
    output.update()
    return output

def assign_world(o,mesh):
    mesh.transform(o.matrix_world.inverted())
    o.data=mesh
    modified.add(o.name)
    if not len(mesh.polygons):empty.add(o.name)

vertices=[]
triangles=[]
for p in pieces:
    if p['family']!='Hull':continue
    o=bpy.data.objects[p['intact']]
    mesh=o.data
    mesh.calc_loop_triangles()
    offset=len(vertices)
    vertices.extend(o.matrix_world@v.co for v in mesh.vertices)
    triangles.extend(tuple(offset+i for i in t.vertices) for t in mesh.loop_triangles)
hull=BVHTree.FromPolygons(vertices,triangles,all_triangles=True)

def width(y,z):
    distances=[]
    for sign in [-1,1]:
        hit=hull.ray_cast(Vector((0,y,z)),Vector((sign,0,0)),10)
        distances.append(abs(hit[0].x)+.02 if hit[0] else 0)
    return min(distances)

profile=[]
for j in range(401):
    y=-20+j*.1
    w=min(width(y,floor_z-.16),width(y,floor_z))
    profile.append((y,w))

contour=[(w,y) for y,w in profile if w>0]
contour+=list(reversed([(-w,y) for y,w in profile if w>0]))

def clip_polygon(polygon,axis,value,above):
    output=[]
    if not polygon:return output
    previous=polygon[-1]
    previous_inside=(previous[axis]>=value) if above else (previous[axis]<=value)
    for point in polygon:
        inside=(point[axis]>=value) if above else (point[axis]<=value)
        if inside!=previous_inside:
            t=(value-previous[axis])/(point[axis]-previous[axis])
            output.append(tuple(previous[j]+t*(point[j]-previous[j]) for j in range(2)))
        if inside:output.append(point)
        previous=point
        previous_inside=inside
    return output

deck_materials=[bpy.data.materials['V4_Deck_Ship_Art_Deck_Worn_Oak_V'+str(i)] for i in range(4)]
floor_vertices=[[] for _ in range(4)]
floor_faces=[[] for _ in range(4)]
floor_uv=[[] for _ in range(4)]
for column in range(-18,18):
    xmin=column*.36
    xmax=(column+1)*.36
    for segment in range(14):
        start=-23.1+segment*3.1+(column%3)*1.033333
        end=min(start+3.1,20)
        start=max(start,-20)
        polygon=contour
        for axis,value,above in [(0,xmin,True),(0,xmax,False),(1,start,True),(1,end,False)]:
            polygon=clip_polygon(polygon,axis,value,above)
        clean=[]
        for point in polygon:
            if not clean or sum((point[j]-clean[-1][j])**2 for j in range(2))>1e-12:clean.append(point)
        if len(clean)>1 and sum((clean[0][j]-clean[-1][j])**2 for j in range(2))<1e-12:clean.pop()
        polygon=clean
        if len(polygon)<3 or end<=start:continue
        material=(column+segment)%4
        vs=floor_vertices[material]
        fs=floor_faces[material]
        uvs=floor_uv[material]
        offset=len(vs)
        count=len(polygon)
        vs.extend((x,y,floor_z-.16) for x,y in polygon)
        vs.extend((x,y,floor_z) for x,y in polygon)
        def face(ids):
            fs.append(tuple(offset+i for i in ids))
            uvs.append([((vs[offset+i][0]-xmin)/.36,(vs[offset+i][1]-start)/3.1) for i in ids])
        face(tuple(reversed(range(count))))
        face(tuple(range(count,count*2)))
        for j in range(count):
            k=(j+1)%count
            face((j,k,k+count,j+count))
for i in range(4):
    mesh=bpy.data.meshes.new('BilgeFloor_Oak_'+str(i))
    mesh.from_pydata(floor_vertices[i],[],floor_faces[i])
    mesh.materials.append(deck_materials[i])
    uv=mesh.uv_layers.new(name='GameUV')
    for polygon,values in zip(mesh.polygons,floor_uv[i]):
        for index,value in zip(polygon.loop_indices,values):uv.data[index].uv=value
    obj=bpy.data.objects.new('V19_BilgeFloor_Oak_'+str(i),mesh)
    collection.objects.link(obj)
    obj.parent=root
    obj['bilge_permanent']=True
    obj['exclude_from_export']=True
    new_objects.append(obj)

mask_size=(256,64)
mask_bounds=(min(v.y for v in vertices),max(v.y for v in vertices),min(v.z for v in vertices)-4.6,8.71-4.6)
mask_widths=[]
for row in range(mask_size[1]):
    z=4.6+mask_bounds[2]+row*(mask_bounds[3]-mask_bounds[2])/(mask_size[1]-1)
    for column in range(mask_size[0]):
        y=mask_bounds[0]+column*(mask_bounds[1]-mask_bounds[0])/(mask_size[0]-1)
        w=width(y,z)
        if not w:
            w=max(width(y+dy,z+dz) for dy,dz in [(0,.02),(0,-.02),(.02,0),(-.02,0)])
        mask_widths.append(round(w,5))
(OUT/'InteriorWaterProfile.json').write_text(json.dumps({'size':mask_size,'bounds':mask_bounds,'widths':mask_widths},separators=(',',':')),encoding='utf-8')

for p in pieces:
    o=bpy.data.objects[p['intact']]
    low,high=bounds(o)
    aperture=p['family']=='Deck' and 'MainDeck' in o.name and low[0]<hole[1] and high[0]>hole[0] and low[1]<hole[3] and high[1]>hole[2]
    hull_piece=p['family']=='Hull' and low[2]<floor_z-.0001
    if hull_piece and high[2]<=floor_z+.0001:
        protected.append(p['piece_id']+10000)
        o['bilge_indestructible']=True
        for name in p['fragment_names']:anchors.append(name)
        continue
    if hull_piece:
        original=world_mesh(o)
        lower=half(original,floor_z,2,False)
        permanent=fresh(o,'V19_Permanent_'+o.name)
        permanent.data=lower
        permanent.matrix_world=Matrix.Identity(4)
        upper=half(original,floor_z,2,True)
        assign_world(o,upper)
        bpy.data.meshes.remove(original)
    if aperture:
        original=world_mesh(o)
        cut=opening(original)
        assign_world(o,cut)
        bpy.data.meshes.remove(original)
    if not hull_piece and not aperture:continue
    mask=0
    for index,name in enumerate(p['fragment_names']):
        fragment=bpy.data.objects[name]
        low,high=bounds(fragment)
        original=world_mesh(fragment)
        if hull_piece:
            if low[2]<floor_z:anchors.append(name)
            cut=half(original,floor_z,2,True)
            if high[2]<=floor_z+.0001:mask|=1<<index
        else:
            cut=opening(original)
        if not len(cut.polygons):mask|=1<<index
        assign_world(fragment,cut)
        bpy.data.meshes.remove(original)
    if mask:masks[str(p['piece_id']+10000)]=mask
    if o.name in empty:protected.append(p['piece_id']+10000)

for p in pieces:
    if 'Exposed_Rib' not in p['intact']:continue
    o=bpy.data.objects[p['intact']]
    low,high=bounds(o)
    if high[0]<hole[0] or low[0]>hole[1] or high[1]<hole[2] or low[1]>hole[3]:continue
    for name in [o.name]+p['fragment_names']:
        obj=bpy.data.objects[name]
        original=world_mesh(obj)
        upper=half(original,floor_z,2,True)
        cut=opening(upper)
        lower=half(original,floor_z,2,False)
        bm=bmesh.new()
        bm.from_mesh(lower)
        bm.from_mesh(cut)
        merged=original.copy()
        bm.to_mesh(merged)
        bm.free()
        assign_world(obj,merged)
        for mesh in [original,upper,cut,lower]:bpy.data.meshes.remove(mesh)

for p in pieces:
    if p['family']!='Woodwork' or any(s in p['intact'] for s in ['Stair','Shroud','HeadCheek','CornerTrim']):continue
    obj=bpy.data.objects[p['intact']]
    low,high=bounds(obj)
    middle_y=(low[1]+high[1])*.5
    underside=9.55 if obj.name=='V3_Bowsprit' else 8.55 if -10<middle_y<10.5 else 9.55 if 10.5<=middle_y<19.7 else None
    if underside is None or not underside-.7<low[2]<underside or high[2]<underside+.15:continue
    for name in [obj.name]+p['fragment_names']:
        part=bpy.data.objects[name]
        original=world_mesh(part)
        assign_world(part,half(original,underside+.005,2,True))
        bpy.data.meshes.remove(original)

for obj in list(bpy.data.objects):
    if obj.type!='MESH' or not (obj.name=='V9_Anchor_Deck_Chain_Outlet' or obj.name.startswith('V3_Polish_Gammon_00_')):continue
    low,high=bounds(obj)
    underside=9.55 if (low[1]+high[1])*.5>10.5 else 8.55
    if not low[2]<underside<high[2]:continue
    original=world_mesh(obj)
    assign_world(obj,half(original,underside+.005,2,True))
    bpy.data.meshes.remove(original)

step_count=18
rise=(8.71-floor_z)/step_count
stair_start=hole[2]+.35
stair_spacing=(hole[3]-.675-stair_start)/(step_count-1)
stair_width=hole[1]-hole[0]
rail_x=stair_width*.5+.05
deck_underside=8.55
for i in range(step_count):
    source=bpy.data.objects['V3_Bow_Step' if i<step_count-1 else 'V3_Bow_Step.004']
    obj=fresh(source,'V19_BilgeStair_Step_'+str(i).zfill(2))
    obj.data.transform(source.matrix_world)
    lo,hi=bounds(source)
    for vertex in obj.data.vertices:
        vertex.co.x=(vertex.co.x-(lo[0]+hi[0])*.5)*stair_width/(hi[0]-lo[0])
        vertex.co.y=stair_start+i*stair_spacing+(vertex.co.y-lo[1])*((.675 if i==step_count-1 else stair_spacing+.02)/(hi[1]-lo[1]))
        vertex.co.z+=floor_z+(i+1)*rise-hi[2]
    obj.matrix_world=Matrix.Identity(4)
    obj['reuse_source']=source.name
longitudinal_scale=(hole[3]-stair_start)/(10.674469947814941-8.45)
stair_slope=rise/stair_spacing
for side in [0,1]:
    for part in ['Stringer','Handrail']:
        source=bpy.data.objects['V3_Bow_Stair_'+part+('.001' if side and part=='Stringer' else '')]
        obj=fresh(source,'V19_BilgeStair_'+part+'_'+str(side))
        obj.data.transform(source.matrix_world)
        lo,hi=bounds(source)
        center_x=(lo[0]+hi[0])*.5
        for vertex in obj.data.vertices:
            t=vertex.co.y-8.45
            vertex.co.x+=(-1 if side==0 else 1)*(rail_x if part=='Handrail' else stair_width*.5-.02)-center_x
            vertex.co.y=stair_start+t*longitudinal_scale
            vertex.co.z=floor_z+rise+(vertex.co.z-8.91)+t*(stair_slope*longitudinal_scale-.2/.42)
        obj.matrix_world=Matrix.Identity(4)
        obj['reuse_source']=source.name
        original=obj.data
        obj.data=half(original,deck_underside if part=='Handrail' else 8.71,2,False)
        bpy.data.meshes.remove(original)
for side in [-1,1]:
    source=bpy.data.objects['V3_Bow_Port_Stair_CarvedSupport_00']
    lo,hi=bounds(source)
    for j in [0,4,8,12]:
        obj=fresh(source,'V19_BilgeStair_Support_'+str(side)+'_'+str(j))
        if j==0:
            for vertex in obj.data.vertices:
                vertex.co.z=lo[2]+(vertex.co.z-lo[2])*(1+.05/(hi[2]-lo[2]))
        shift=Vector((side*rail_x-(lo[0]+hi[0])*.5,stair_start+j*stair_spacing-(lo[1]+hi[1])*.5,floor_z+j*rise+(rise-.2)-lo[2]))
        obj.matrix_world=Matrix.Translation(shift)@source.matrix_world
        original=obj.data
        mesh=world_mesh(obj)
        obj.data=half(mesh,deck_underside,2,False)
        obj.matrix_world=Matrix.Identity(4)
        bpy.data.meshes.remove(mesh)
        bpy.data.meshes.remove(original)

def fitted_copy(source,name,center,size):
    obj=fresh(source,name)
    obj.data.transform(source.matrix_world)
    lo,hi=bounds(source)
    for vertex in obj.data.vertices:
        vertex.co=Vector(center)+Vector([(vertex.co[i]-(lo[i]+hi[i])*.5)*size[i]/max(.0001,hi[i]-lo[i]) for i in range(3)])
    obj.matrix_world=Matrix.Identity(4)
    obj['reuse_source']=source.name
    return obj

plank_source=bpy.data.objects['V3_Bow_Step']
guard_height=.32
for side in [-1,1]:
    board=fitted_copy(plank_source,'V19_BilgeHatchGuard_Board_'+str(side)+'_0',(side*.81,(hole[2]+hole[3])*.5,8.71+guard_height*.5),(.12,hole[3]-hole[2],guard_height))
    for vertex in board.data.vertices:
        t=(vertex.co.y-hole[2])/(hole[3]-hole[2])
        start=hole[2]-(abs(vertex.co.x)-.75)
        vertex.co.y=start+(hole[3]-start)*t
board=fitted_copy(plank_source,'V19_BilgeHatchGuard_AftBoard',(0,hole[2]-.06,8.71+guard_height*.5),(1.74,.12,guard_height))
for vertex in board.data.vertices:vertex.co.x*= (.75+hole[2]-vertex.co.y)/.87
fitted_copy(plank_source,'V19_BilgeHatchGuard_EntrySill',(0,hole[3]+.08,8.77),(1.5,.16,.12))

pump=bpy.data.objects.new('V19_BilgePump',None)
collection.objects.link(pump)
pump.parent=root
pump.location=(0,0,floor_z)
pump['exclude_from_export']=True
pump['bilge_permanent']=True
new_objects.append(pump)
body_source=bpy.data.objects['V19_Pump_Body_Source_0']
body=fresh(body_source,'V19_PumpBody',pump)
body.data.transform(body_source.matrix_world)
body.matrix_parent_inverse=Matrix.Identity(4)
body.location=(0,0,0)
body.rotation_euler=(0,0,0)
body.scale=(1.17,)*3
hinge=bpy.data.objects.new('V19_PumpLeverPivot',None)
collection.objects.link(hinge)
hinge.parent=pump
hinge.location=(0,0,.907*1.17)
hinge['exclude_from_export']=True
new_objects.append(hinge)
source=bpy.data.objects['V19_Pump_Lever_Source_0']
lever=fresh(source,'V19_PumpLever',hinge)
lever.data.transform(source.matrix_world)
lever.data.transform(Matrix.Translation(Vector((.399,0,-.673))))
rotation=Matrix.Rotation(math.pi,4,'X')@Matrix.Rotation(math.pi*.5,4,'Z')
lever.data.transform(rotation)
lever.matrix_parent_inverse=Matrix.Identity(4)
lever.location=(0,0,0)
lever.rotation_euler=(0,0,0)
lever.scale=(.8,)*3
grip=bpy.data.objects.new('V19_PumpGrip',None)
collection.objects.link(grip)
grip.parent=hinge
grip.location=(0,-.61,.45)
grip['exclude_from_export']=True
new_objects.append(grip)
for obj in [body_source,source]:obj.hide_set(True);obj.hide_render=True

for obj in bpy.data.objects:
    if not obj.get('display_only') or obj.type!='MESH':continue
    source_names=json.loads(obj.get('source_objects','[]'))
    if not any(name in modified for name in source_names):continue
    low,high=bounds(obj)
    if low[0]<hole[1] and high[0]>hole[0] and low[1]<hole[3] and high[1]>hole[2] and 'Deck_' in obj.name:
        original=world_mesh(obj)
        obj.data=opening(original)
        obj.matrix_world=Matrix.Identity(4)
        bpy.data.meshes.remove(original)

root['bilge_deck_version']=6
root['bilge_floor_z']=floor_z
root['bilge_aperture']=list(hole)
hawse=bpy.data.objects['V9_Anchor_Chain_Swing_Pivot'].matrix_world.translation
root['bilge_anchor_route']=[(1.45,15.7,9.79),(1.45,15.7,9.48),list(hawse)]
curve=bpy.data.objects['V9_Anchor_Chain_Route']
points=curve.data.splines[0].points
start=curve.matrix_world@Vector(points[75].co[:3])
under=Vector((1.45,15.7,9.48))
inverse=curve.matrix_world.inverted()
for i in range(75,87):points[i].co=(*inverse@start.lerp(under,(i-75)/11),1)
for i in range(86,177):points[i].co=(*inverse@under.lerp(hawse,(i-86)/90),1)
for file in (OUT/'Meshes').glob('*.asset'):
    name=file.stem
    if name.startswith(('V19_','BilgeBatch')):continue
    obj=bpy.data.objects.get(name)
    if obj and obj.type=='MESH':modified.add(name)
empty={name for name in modified if not len(bpy.data.objects[name].data.polygons)}
manifest={'version':3,'waterline':4.6,'floor_z':floor_z,'hole':hole,'stair_width':stair_width,'rail_ceiling_z':deck_underside,'protected_sections':sorted(set(protected)),'protected_fragments':masks,'foundation_fragments':sorted(set(anchors)),'modified_objects':sorted(modified),'empty_objects':sorted(empty),'new_objects':[o.name for o in new_objects], 'pump_pivot':[0,0,.907*1.17],'pump_grip':[0,-.61,.45],'pump_down_angle':82}
manifest['version']=6
(OUT/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.view_layer.update()
trim_script=BASE/'Tools/ShipV3/ClipHoldHullTrim.py'
exec(compile(trim_script.read_text(encoding='utf-8'),str(trim_script),'exec'))
seam_script=BASE/'Tools/ShipV3/SealInteriorShipSeams.py'
exec(compile(seam_script.read_text(encoding='utf-8'),str(seam_script),'exec'))
end_script=BASE/'Tools/ShipV3/RepairHoldInterior.py'
exec(compile(end_script.read_text(encoding='utf-8'),str(end_script),'exec'))
skin_script=BASE/'Tools/ShipV3/ClipHullTrimToSkin.py'
exec(compile(skin_script.read_text(encoding='utf-8'),str(skin_script),'exec'))
result={'version':13,'floor_z':floor_z,'modified':len(manifest['modified_objects']),'new':len(new_objects),'protected_sections':len(set(protected)),'empty':len(manifest['empty_objects']),'manifest':str(OUT/'BilgeDeck.json')}
