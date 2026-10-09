import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
EXPORT=ROOT/'PirateGame'/'Assets'/'Models'/'World'/'CoastalEnvironment'
NAMES=['Sea_Lagoon_Cave','Reef_Moai_A','Reef_Spires_A','Reef_Spires_B','Reef_Spires_C','SeaArch_Huge_A','Reef_ShallowField_A','RockLarge','RockMedium','CliffWallA','CliffWallB','CliffWallC']
selection=globals().get('TRIPO_COLLISION_NAMES',['SeaArch_Huge_A','Sea_Lagoon_Cave'])
protected={
    'SeaArch_Huge_A':[((-32,-28,0),(30,28,55))],
    'Sea_Lagoon_Cave':[((-20,-25,0),(20,25,95)),((-10,-140,0),(10,140,27)),((-145,-15,0),(145,15,30))]
}
targets={'SeaArch_Huge_A':4000,'Sea_Lagoon_Cave':4000,'Reef_Moai_A':1000,'Reef_Spires_A':1800,'Reef_Spires_B':2600,'Reef_Spires_C':3000,'Reef_ShallowField_A':900,'RockLarge':450,'RockMedium':350,'CliffWallA':1000,'CliffWallB':1200,'CliffWallC':1000}
previous=bpy.context.window.scene
source_scene=bpy.data.scenes['TripoCoastalCollection']
visibility={coll.name:coll.hide_viewport for coll in source_scene.collection.children}
scene=bpy.data.scenes.get('TripoCollisionExport') or bpy.data.scenes.new('TripoCollisionExport')
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
EXPORT.mkdir(parents=True,exist_ok=True)
matrices={}

def activate(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]

def remove(obj):
    mesh=obj.data if obj.type=='MESH' else None
    bpy.data.objects.remove(obj,do_unlink=True)
    if mesh and mesh.users==0:
        bpy.data.meshes.remove(mesh)

def clean():
    for obj in list(scene.objects):
        if obj.name.startswith('TripoCollisionExport_'):
            remove(obj)

def bounds(obj):
    transform=obj.matrix_basis if obj.parent is None and not obj.constraints else obj.matrix_world
    points=[transform@vertex.co for vertex in obj.data.vertices]
    return [[min(point[i] for point in points) for i in range(3)],[max(point[i] for point in points) for i in range(3)]]

def triangles(obj):
    return sum(len(poly.vertices)-2 for poly in obj.data.polygons)

def triangle_box(points,low,high):
    epsilon=.0001
    if any(max(point[i] for point in points)<=low[i]+epsilon or min(point[i] for point in points)>=high[i]-epsilon for i in range(3)):
        return False
    center=Vector(tuple((low[i]+high[i])*.5 for i in range(3)))
    half=Vector(tuple((high[i]-low[i])*.5 for i in range(3)))
    vertices=[point-center for point in points]
    edges=[vertices[1]-vertices[0],vertices[2]-vertices[1],vertices[0]-vertices[2]]
    axes=[edges[0].cross(edges[1])]
    base=[Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))]
    axes.extend(edge.cross(axis) for edge in edges for axis in base)
    for axis in axes:
        if axis.length_squared<1e-16:
            continue
        axis.normalize()
        projections=[point.dot(axis) for point in vertices]
        radius=sum(abs(axis[i])*half[i] for i in range(3))
        if max(projections)<=-radius+epsilon or min(projections)>=radius-epsilon:
            return False
    return True

def passage_hits(obj,name):
    boxes=protected.get(name,[])
    if not boxes:
        return []
    transform=obj.matrix_basis if obj.parent is None and not obj.constraints else obj.matrix_world
    points=[transform@vertex.co for vertex in obj.data.vertices]
    obj.data.calc_loop_triangles()
    hits=[]
    for triangle in obj.data.loop_triangles:
        vertices=[points[index] for index in triangle.vertices]
        for index,(low,high) in enumerate(boxes):
            if triangle_box(vertices,low,high):
                hits.append({'triangle':triangle.index,'protectedBox':index})
                if len(hits)>=10:
                    return hits
    return hits

def copy_source(source,name):
    obj=source.copy()
    obj.data=source.data.copy()
    obj.name=name
    obj.parent=None
    scene.collection.objects.link(obj)
    obj.hide_render=False
    obj.hide_viewport=False
    obj.hide_set(False)
    obj.matrix_world=matrices[source.name]
    activate([obj])
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.data.materials.clear()
    for layer in list(obj.data.uv_layers):
        obj.data.uv_layers.remove(layer)
    return obj

def collision_copy(reference,name,ratio):
    obj=reference.copy()
    obj.data=reference.data.copy()
    obj.name='TripoCollisionExport_'+name+'_Rock_COL'
    scene.collection.objects.link(obj)
    obj.hide_set(False)
    activate([obj])
    if ratio<.999:
        modifier=obj.modifiers.new('VisualMatchedCollision','DECIMATE')
        modifier.ratio=ratio
        modifier.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj

try:
    if any(name not in NAMES for name in selection):
        raise RuntimeError('Collision selection must contain rock models only')
    bpy.context.window.scene=source_scene
    for coll in source_scene.collection.children:
        coll.hide_viewport=False
    bpy.context.view_layer.update()
    for name in selection:
        for obj in bpy.data.collections['TripoSet_'+name].objects:
            if obj.type=='MESH' and obj.get('role')=='Rock':
                matrices[obj.name]=obj.matrix_basis.copy() if obj.parent is None and not obj.constraints else obj.matrix_world.copy()
    bpy.context.window.scene=scene
    records=[]
    for name in selection:
        clean()
        sources=[obj for obj in bpy.data.collections['TripoSet_'+name].objects if obj.type=='MESH' and obj.get('role')=='Rock']
        if not sources:
            raise RuntimeError('No visible rock geometry: '+name)
        hidden=[]
        for source in sources:
            high=max((matrices[source.name]@vertex.co).z for vertex in source.data.vertices)
            if high<-.0001:
                hidden.append(source.name)
        if hidden:
            raise RuntimeError('Fully submerged isolated source modules must be removed before collision export: '+str(hidden))
        copies=[copy_source(source,'TripoCollisionExport_'+name+'_Source_'+str(index)) for index,source in enumerate(sources)]
        activate(copies)
        bpy.ops.object.join()
        reference=copies[0]
        reference.name='TripoCollisionExport_'+name+'_Reference'
        bm=bmesh.new()
        bm.from_mesh(reference.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(reference.data)
        bm.free()
        reference.data.update()
        source_bounds=bounds(reference)
        source_triangles=triangles(reference)
        initial_hits=passage_hits(reference,name)
        if initial_hits:
            raise RuntimeError('Visible source intrudes into protected passage: '+name+' '+str(initial_hits))
        target=targets[name]
        initial=min(1.0,target/max(1,source_triangles))
        ratios=sorted(set([initial,max(initial,.2),max(initial,.35),max(initial,.6),1.0]))
        accepted=None
        attempts=[]
        for ratio in ratios:
            obj=collision_copy(reference,name,ratio)
            hits=passage_hits(obj,name)
            attempts.append({'ratio':ratio,'triangles':triangles(obj),'protectedHits':len(hits)})
            if not hits:
                accepted=obj
                break
            remove(obj)
        if accepted is None:
            raise RuntimeError('Unable to simplify collision without protected passage overlap: '+name)
        exported_bounds=bounds(accepted)
        accepted.scale.x*=-1
        activate([accepted])
        bpy.context.view_layer.update()
        path=EXPORT/(name+'_COL.fbx')
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',use_space_transform=True,bake_space_transform=True,axis_forward='-Z',axis_up='Y',mesh_smooth_type='OFF',use_mesh_modifiers=True,use_tspace=False,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False)
        records.append({'name':name,'path':str(path),'sourceRockObjects':len(sources),'sourceRockTriangles':source_triangles,'collisionTriangles':triangles(accepted),'sourceBoundsBlender':source_bounds,'collisionBoundsBlender':exported_bounds,'attempts':attempts,'protectedPassagesPassed':True,'axisCorrectionX':True,'fullySubmergedIsolatedModules':0,'plantGeometryIncluded':False,'sourceMeshesUnchanged':True})
        clean()
    report_path=OUTPUT/'tripo-collision-export-manifest.json'
    previous_records=json.loads(report_path.read_text(encoding='utf-8')) if report_path.exists() else []
    previous_records=[record for record in previous_records if record['name'] not in selection]
    previous_records.extend(records)
    report_path.write_text(json.dumps(previous_records,ensure_ascii=False,indent=2),encoding='utf-8')
    result={'exported':records,'legacyColliderAssetsUntouched':True,'collisionMatchesCurrentRockVisuals':True}
finally:
    clean()
    for coll in source_scene.collection.children:
        if coll.name in visibility:
            coll.hide_viewport=visibility[coll.name]
    bpy.context.window.scene=previous
