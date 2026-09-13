import bpy
import bmesh
import json
import os
from mathutils import Vector

base = 'C:/Users/K/Project'
folder = base + '/Assets/Models/Ships/MainShip/Destruction'
os.makedirs(folder, exist_ok=True)
old_collection = bpy.data.collections.get('F2_Destruction_Editable')
if old_collection:
    for obj in list(old_collection.objects): bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(old_collection)
collection = bpy.data.collections.new('F2_Destruction_Editable')
bpy.context.scene.collection.children.link(collection)
fresh = bpy.data.materials.get('FreshSplitWood') or bpy.data.materials.new('FreshSplitWood')
fresh.diffuse_color = (0.56, 0.31, 0.12, 1)
records = []
exported = []

def close(bm, material):
    wires = [e for e in bm.edges if e.is_wire]
    if wires:
        bmesh.ops.delete(bm, geom=wires, context='EDGES')
    edges = [e for e in bm.edges if e.is_boundary]
    if edges:
        for face in bmesh.ops.holes_fill(bm, edges=edges, sides=0)['faces']:
            face.material_index = material
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))

def cut(bm, axis, value, upper):
    point = Vector((0,0,0)); point[axis] = value
    normal = Vector((0,0,0)); normal[axis] = 1
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces), dist=0.00001, plane_co=point, plane_no=normal, clear_outer=not upper, clear_inner=upper)

def mesh_object(name, bm, materials):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh); mesh.update()
    obj = bpy.data.objects.new(name, mesh); collection.objects.link(obj)
    for mat in materials: mesh.materials.append(mat)
    exported.append(obj)
    return obj

def section(identifier, label, source_name, limits, kind, supports=(), preserve=False):
    source = bpy.data.objects[source_name]
    materials = list(source.data.materials)+[fresh]
    fresh_index = len(materials)-1
    bm = bmesh.new(); bm.from_mesh(source.data); bm.transform(source.matrix_world)
    remaining=set(bm.faces)
    components=[]
    while remaining:
        first=remaining.pop();component={first};pending=[first]
        while pending:
            face=pending.pop()
            for edge in face.edges:
                for adjacent in edge.link_faces:
                    if adjacent in remaining:
                        remaining.remove(adjacent);component.add(adjacent);pending.append(adjacent)
        components.append(component)
    for component in components:
        if any(edge.is_boundary for face in component for edge in face.edges):
            bmesh.ops.solidify(bm,geom=list(component),thickness=.14)
    close(bm, fresh_index)
    for axis, lo, hi in limits:
        if lo is not None: cut(bm, axis, lo, True)
        if hi is not None: cut(bm, axis, hi, False)
        close(bm, fresh_index)
    if not bm.faces:
        bm.free(); raise RuntimeError('Empty section '+label)
    prefix = 'SD_%03d_' % identifier
    intact = mesh_object(prefix+'Intact', bm, materials)
    coords = [v.co.copy() for v in bm.verts]
    low = Vector([min(v[i] for v in coords) for i in range(3)])
    high = Vector([max(v[i] for v in coords) for i in range(3)])
    center = (low+high)*.5
    axis = 2 if kind in ('Mast','Yard','Bowsprit') else 1
    width = high[axis]-low[axis]
    chunks = []
    chunk_axis = 0 if kind in ('Mast','Yard','Bowsprit') else 2
    chunk_width = high[chunk_axis]-low[chunk_axis]
    for index in range(0 if preserve else 4):
        piece = bm.copy()
        cut(piece, axis, high[axis]-width*.65, True)
        cut(piece, chunk_axis, low[chunk_axis]+chunk_width*index/4, True)
        cut(piece, chunk_axis, low[chunk_axis]+chunk_width*(index+1)/4, False)
        close(piece,fresh_index)
        if piece.faces:
            chunks.append(mesh_object(prefix+'Chunk%d'%index,piece,materials))
        piece.free()
    for state, factor in [('Damaged',.16),('Critical',.38),('Destroyed',.65)]:
        variant = bm.copy()
        if preserve:
            for vertex in variant.verts:
                if vertex.co.z > center.z:
                    vertex.co.z -= factor*.12
        else:
            length = (high-low)[axis]
            threshold = high[axis]-length*factor
            cut(variant,axis,threshold,False)
            close(variant,fresh_index)
            for vertex in variant.verts:
                if abs(vertex.co[axis]-threshold)<.0001:
                    other = vertex.co[(axis+1)%3]*9.7+vertex.co[(axis+2)%3]*3.1
                    import math
                    vertex.co[axis] += math.sin(other)*min(.22,length*.05)
            bmesh.ops.recalc_face_normals(variant,faces=list(variant.faces))
        mesh_object(prefix+state,variant,materials);variant.free()
    record = {'id':identifier,'name':label,'source':source_name,'kind':kind,'supports':list(supports),'preserve':preserve,'center':[center.x,center.z,-center.y], 'breach':[center.x, max(low.z, -.6),-center.y], 'chunks':len(chunks)}
    records.append(record); bm.free()

for side, offset in [(-1,100),(1,110)]:
    for index in range(6):
        limits=[(1,-19+index*38/6,-19+(index+1)*38/6),(0,None,0)] if side<0 else [(1,-19+index*38/6,-19+(index+1)*38/6),(0,0,None)]
        section(offset+index,('Port' if side<0 else 'Starboard')+'Hull'+str(index+1),'F2_Body',limits,'Hull')
section(120,'BowHull','F2_Body',[(1,None,-19)],'Hull')
section(121,'SternHull','F2_Body',[(1,19,None)],'Hull')
for identifier,name in [(200,'F2_FloorFront'),(201,'F2_FloorMid'),(202,'F2_FloorBackBottom'),(203,'F2_FloorBackTop')]:
    section(identifier,name[3:],name,[],'Deck',preserve=True)
for identifier,name,height in [(300,'F2_MastFront',17),(310,'F2_MastMid',23),(320,'F2_MastBack',20)]:
    section(identifier,name[3:],name,[(2,None,height)],'Mast',preserve=name=='F2_MastMid')
    section(identifier+1,name[3:]+'Upper',name,[(2,height,None)],'Yard',supports=(identifier,))
section(330,'Bowsprit','F2_Prow',[],'Bowsprit')
section(400,'Rudder','F2_Rudder',[],'Rudder')
for identifier,name,parent in [(500,'F2_FencingFront',200),(501,'F2_FencingMid',201),(502,'F2_FencingBack',203)]:
    section(identifier,name[3:],name,[],'Railing',supports=(parent,))
for name,point in [('Anchor_Origin',(0,0,0)),('Anchor_Right',(1,0,0)),('Anchor_Up',(0,0,1)),('Anchor_Forward',(0,-1,0))]:
    obj=bpy.data.objects.new(name,None);collection.objects.link(obj);obj.location=point;exported.append(obj)
for obj in exported:
    if obj.type != 'MESH': continue
    bm=bmesh.new();bm.from_mesh(obj.data)
    needs_shell=any(e.is_boundary for e in bm.edges);bm.free()
    if needs_shell:
        bpy.context.view_layer.objects.active=obj
        modifier=obj.modifiers.new('ClosedWoodSurface','SOLIDIFY')
        modifier.thickness=.025;modifier.solidify_mode='NON_MANIFOLD'
        modifier.material_offset=len(obj.data.materials)-1
        bpy.ops.object.modifier_apply(modifier=modifier.name)
for obj in bpy.context.selected_objects: obj.select_set(False)
for obj in exported: obj.select_set(True)
bpy.context.view_layer.objects.active=exported[0]
bpy.ops.export_scene.fbx(filepath=folder+'/MainShipDestruction.fbx',use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
with open(folder+'/sections.json','w',encoding='utf-8') as stream: json.dump({'sections':records},stream,indent=2)
for obj in exported:
    obj.hide_set(not obj.name.endswith('Intact'))
bpy.data.collections['F2_Draft_Editable'].hide_viewport=True
bpy.data.collections['F2_Draft_Editable'].hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=base+'/Art/Blender/Ships/F2Draft/F2_Ship_Destruction.blend')
result={'sections':len(records),'objects':len(exported),'blend':bpy.data.filepath,'fbx':folder+'/MainShipDestruction.fbx'}
