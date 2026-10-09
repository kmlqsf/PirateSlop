import bpy
import json
import hashlib
from pathlib import Path
from mathutils import Matrix

ROOT=Path(r'D:\projects\Pirate_BR')
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
OUTPUT=ROOT/'output'/'CoastalEnvironment'
EXPORT=ROOT/'PirateGame'/'Assets'/'Models'/'World'/'CoastalEnvironment'
TEXTURES=EXPORT/'Textures'/'Tripo'
all_names=[record['name'] for record in json.loads((OUTPUT/'physical-shape-profiles.json').read_text(encoding='utf-8-sig'))]
selection=globals().get('TRIPO_EXPORT_NAMES',all_names)
previous=bpy.context.window.scene
source_scene=bpy.data.scenes['TripoCoastalCollection']
source_visibility={coll.name:coll.hide_viewport for coll in source_scene.collection.children}
source_matrices={}
scene=bpy.data.scenes.get('TripoCollectionExport') or bpy.data.scenes.new('TripoCollectionExport')
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
EXPORT.mkdir(parents=True,exist_ok=True)
TEXTURES.mkdir(parents=True,exist_ok=True)

def activate(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]

def clean():
    for obj in list(scene.objects):
        if obj.name.startswith('TripoExport_'):
            mesh=obj.data if obj.type=='MESH' else None
            bpy.data.objects.remove(obj,do_unlink=True)
            if mesh and mesh.users==0:
                bpy.data.meshes.remove(mesh)

def copy_world(source,name):
    obj=source.copy()
    obj.data=source.data.copy()
    obj.name=name
    obj.parent=None
    scene.collection.objects.link(obj)
    obj.hide_render=False
    obj.hide_viewport=False
    obj.hide_set(False)
    obj.matrix_world=source_matrices[source.name]
    activate([obj])
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    return obj

def write_maps():
    materials={}
    for model in all_names:
        coll=bpy.data.collections.get('TripoSet_'+model)
        if not coll:
            continue
        for obj in coll.objects:
            for slot in obj.material_slots:
                mat=slot.material
                if not mat or mat.name in materials or not mat.use_nodes:
                    continue
                images=[]
                for node in mat.node_tree.nodes:
                    if node.type!='TEX_IMAGE' or not node.image:
                        continue
                    image=node.image
                    if not image.packed_file:
                        image.pack()
                    data=bytes(image.packed_file.data)
                    filename=Path(image.name).name
                    if Path(filename).suffix.lower() not in {'.png','.jpg','.jpeg','.tga','.exr'}:
                        extension=Path(image.filepath).suffix.lower()
                        filename=filename.replace('.','_')+(extension if extension in {'.png','.jpg','.jpeg','.tga','.exr'} else '.png')
                    path=TEXTURES/filename
                    if not path.exists() or hashlib.md5(path.read_bytes()).digest()!=hashlib.md5(data).digest():
                        path.write_bytes(data)
                    lower=image.name.lower()
                    kind='Normal' if 'normal' in lower or 'nor_' in lower else 'MetallicRoughness' if '_rm.' in lower else 'Alpha' if 'alpha' in lower else 'BaseColor'
                    images.append({'image':image.name,'kind':kind,'path':str(path),'assetPath':path.relative_to(ROOT/'PirateGame').as_posix(),'colorSpace':image.colorspace_settings.name,'size':list(image.size),'bytes':len(data),'md5':hashlib.md5(data).hexdigest()})
                materials[mat.name]={'material':mat.name,'images':images}
    (OUTPUT/'tripo-material-map-manifest.json').write_text(json.dumps(list(materials.values()),ensure_ascii=False,indent=2),encoding='utf-8')
    return len(materials)

def lod_ratio(model,role,level,triangles):
    if level==0:
        return 1.0
    if role=='Palm':
        return (.75,.50)[level-1]
    if role=='Fern':
        return (.65,.40)[level-1]
    ratio=(.40,.12)[level-1]
    if level==1:
        cap=30000 if model.startswith('Sea') else 16000 if model.startswith('Reef_Spires') else 6000 if model.startswith('Reef') else 3500
    else:
        cap=5000 if model.startswith('Sea') else 2500 if model.startswith('Reef_Spires') else 1800 if model.startswith('Reef') else 600 if model.startswith('Cliff') else 250
    return min(ratio,cap/max(1,triangles))

try:
    bpy.context.window.scene=source_scene
    for coll in source_scene.collection.children:
        coll.hide_viewport=False
    bpy.context.view_layer.update()
    for name in selection:
        for obj in bpy.data.collections['TripoSet_'+name].objects:
            source_matrices[obj.name]=obj.matrix_basis.copy() if obj.parent is None and not obj.constraints else obj.matrix_world.copy()
    bpy.context.window.scene=scene
    material_count=write_maps()
    records=[]
    for name in selection:
        clean()
        coll=bpy.data.collections['TripoSet_'+name]
        references={}
        for role in ['Rock','Palm','Fern']:
            sources=[obj for obj in coll.objects if obj.type=='MESH' and obj.get('role')==role]
            if not sources:
                continue
            copies=[copy_world(obj,'TripoExport_'+name+'_'+role+'_'+str(index)) for index,obj in enumerate(sources)]
            activate(copies)
            bpy.ops.object.join()
            reference=copies[0]
            reference.name='TripoExport_'+name+'_'+role+'_Reference'
            reference.hide_render=True
            references[role]=reference
        for level in range(3):
            objects=[]
            for role,reference in references.items():
                obj=reference.copy()
                obj.data=reference.data.copy()
                obj.name='TripoExport_'+name+'_'+role+'_LOD'+str(level)
                scene.collection.objects.link(obj)
                obj.hide_render=False
                obj.hide_set(False)
                activate([obj])
                triangles=sum(len(poly.vertices)-2 for poly in obj.data.polygons)
                ratio=lod_ratio(name,role,level,triangles)
                if ratio<1:
                    modifier=obj.modifiers.new('NativeUVLOD','DECIMATE')
                    modifier.ratio=ratio
                    modifier.use_collapse_triangulate=True
                    bpy.ops.object.modifier_apply(modifier=modifier.name)
                    normal=obj.modifiers.new('NativeSurfaceNormals','DATA_TRANSFER')
                    normal.object=reference
                    normal.use_loop_data=True
                    normal.data_types_loops={'CUSTOM_NORMAL'}
                    normal.loop_mapping='POLYINTERP_NEAREST'
                    bpy.ops.object.modifier_apply(modifier=normal.name)
                obj.scale.x *= -1
                objects.append(obj)
            activate(objects)
            path=EXPORT/(name+'_LOD'+str(level)+'.fbx')
            bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',use_space_transform=True,bake_space_transform=True,axis_forward='-Z',axis_up='Y',mesh_smooth_type='OFF',use_mesh_modifiers=True,use_tspace=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False)
            records.append({'name':name,'lod':level,'path':str(path),'triangles':sum(sum(len(poly.vertices)-2 for poly in obj.data.polygons) for obj in objects),'renderers':len(objects),'submeshes':sum(len(obj.material_slots) for obj in objects),'materials':sorted({slot.material.name for obj in objects for slot in obj.material_slots if slot.material}),'boundsBlender':[[min((obj.matrix_world@vertex.co)[i] for obj in objects for vertex in obj.data.vertices) for i in range(3)],[max((obj.matrix_world@vertex.co)[i] for obj in objects for vertex in obj.data.vertices) for i in range(3)]],'UVs':sorted({layer.name for obj in objects for layer in obj.data.uv_layers})})
            for obj in objects:
                mesh=obj.data
                bpy.data.objects.remove(obj,do_unlink=True)
                if mesh.users==0:
                    bpy.data.meshes.remove(mesh)
        clean()
    previous_records=[]
    report_path=OUTPUT/'tripo-collection-export-manifest.json'
    if report_path.exists():
        previous_records=json.loads(report_path.read_text(encoding='utf-8'))
    previous_records=[record for record in previous_records if record['name'] not in selection]
    previous_records.extend(records)
    report_path.write_text(json.dumps(previous_records,ensure_ascii=False,indent=2),encoding='utf-8')
    result={'exported':records,'materialMaps':material_count,'sourceMeshesUnchanged':True,'existingMetaFilesUntouched':True}
finally:
    clean()
    for coll in source_scene.collection.children:
        if coll.name in source_visibility:
            coll.hide_viewport=source_visibility[coll.name]
    bpy.context.window.scene=previous
