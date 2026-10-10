import bpy
import bmesh
import json
import shutil
from pathlib import Path
from mathutils import Matrix

base=Path('D:/projects/Pirate_BR/PirateGame')
out=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((out/'BilgeDeck.json').read_text(encoding='utf-8'))
old_scene=bpy.context.window.scene
for name in manifest['new_objects']:
    obj=bpy.data.objects[name]
    if not name.startswith('V19_BilgeFloor'):continue
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
sources=[bpy.data.objects[n] for n in manifest['modified_objects']+manifest['new_objects'] if n not in manifest['empty_objects']]
scene=bpy.data.scenes.new('BilgeExportTemporary')
renamed=[]
copies={}
bpy.context.window.scene=scene
graph=old_scene.view_layers[0].depsgraph
for obj in sources:
    copy=obj.copy()
    copy.animation_data_clear()
    if obj.type=='MESH':
        copy.data=bpy.data.meshes.new_from_object(obj.evaluated_get(graph),preserve_all_data_layers=True,depsgraph=graph)
        copy.modifiers.clear()
    copy.parent=None
    copy.matrix_world=obj.matrix_world.copy()
    copy.hide_viewport=copy.hide_render=copy.hide_select=False
    scene.collection.objects.link(copy)
    copies[obj.name]=copy
for obj in sources:
    copy=copies[obj.name]
    matrix=copy.matrix_world.copy()
    copy.parent=copies.get(obj.parent.name) if obj.parent else None
    copy.matrix_parent_inverse=Matrix.Identity(4)
    copy.matrix_world=matrix
for obj in sources:
    name=obj.name
    obj.name='__BilgeSource_'+name
    renamed.append((obj,name))
    copies[name].name=name
for name,position in [('BilgeOrigin',(0,0,0)),('BilgeForward',(0,1,0)),('BilgeRight',(1,0,0)),('BilgeUp',(0,0,1))]:
    obj=bpy.data.objects.new(name,None)
    scene.collection.objects.link(obj)
    obj.location=position
scene.view_layers[0].update()
for obj in scene.objects:obj.select_set(True)
scene.view_layers[0].objects.active=next(iter(copies.values()))
try:
    bpy.ops.export_scene.fbx(filepath=str(out/'BilgeDeck.fbx'),check_existing=False,use_selection=True,object_types={'EMPTY','MESH'},global_scale=1,apply_unit_scale=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=False,bake_anim=False,path_mode='STRIP',mesh_smooth_type='OFF')
finally:
    bpy.context.window.scene=old_scene
    temporary_objects=list(scene.objects)
    temporary_meshes=[obj.data for obj in temporary_objects if obj.type=='MESH']
    bpy.data.batch_remove(ids=temporary_objects)
    bpy.data.batch_remove(ids=[mesh for mesh in temporary_meshes if mesh.users==0])
    for obj,name in renamed:obj.name=name
    bpy.data.scenes.remove(scene)
textures=out/'Textures'
textures.mkdir(exist_ok=True)
for part in ['Body','Lever']:
    src=base/'Art/Blender/Ships/BilgePump/Sources'/part
    for file in src.rglob('*'):
        if not file.is_file() or file.suffix.lower() not in ['.jpeg','.png']:continue
        suffix=file.stem.split('_')[-1]
        if suffix=='rm':continue
        shutil.copy2(file,textures/(part+'_'+suffix+file.suffix.lower()))
settings=bpy.context.preferences.filepaths
saved_versions=settings.save_version
settings.save_version=0
try:
    bpy.ops.wm.save_as_mainfile(filepath=str(base/'Art/Blender/Ships/ShipV3/ShipV3_Fitted.blend'))
finally:settings.save_version=saved_versions
result={'fbx':str(out/'BilgeDeck.fbx'),'saved_source':bpy.data.filepath,'objects':len(sources)}
