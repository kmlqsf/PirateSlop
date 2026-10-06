import bpy
import json
import os
from mathutils import Vector
previous=bpy.context.scene
scene=bpy.data.scenes.new('CoastalModularArch')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
masters=bpy.data.collections.new('NativeCC0Masters')
scene.collection.children.link(masters)
report=[]
for key,source_name,target in [
    ('Cliff','QC_coastal_cliff_02_coastal_cliff_02_LOD3',4000),
    ('RockFace','QC_rock_face_02_rock_face_02',1800),
    ('Moss01','QC_rock_moss_set_01_rock_moss_set_01_rock01',650),
    ('Moss03','QC_rock_moss_set_01_rock_moss_set_01_rock03',450),
    ('Moss05','QC_rock_moss_set_01_rock_moss_set_01_rock05',650)
]:
    source=bpy.data.objects[source_name]
    obj=source.copy()
    obj.data=source.data.copy()
    obj.name='CC0Master_'+key
    masters.objects.link(obj)
    obj.hide_render=False
    obj.hide_set(False)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    original_tris=sum(len(p.vertices)-2 for p in obj.data.polygons)
    modifier=obj.modifiers.new('PreserveScanShape','DECIMATE')
    modifier.decimate_type='COLLAPSE'
    modifier.ratio=min(1,target/original_tris)
    modifier.use_collapse_triangulate=True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    modifier=obj.modifiers.new('PreserveNativeNormals','DATA_TRANSFER')
    modifier.object=source
    modifier.use_loop_data=True
    modifier.data_types_loops={'CUSTOM_NORMAL'}
    modifier.loop_mapping='POLYINTERP_NEAREST'
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    matrix=obj.matrix_world.copy()
    for vertex in obj.data.vertices:vertex.co=matrix @ vertex.co
    obj.matrix_world.identity()
    points=[v.co for v in obj.data.vertices]
    low=Vector(tuple(min(v[i] for v in points) for i in range(3)))
    high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
    center=(low+high)*.5
    for vertex in obj.data.vertices:vertex.co-=center
    obj.data.update()
    obj.hide_render=True
    obj['ModuleKey']=key
    obj['PreparedTargetTris']=target
    obj['NativeUVPreserved']=True
    report.append({'key':key,'source':source_name,'originalTris':original_tris,'preparedTris':sum(len(p.vertices)-2 for p in obj.data.polygons),'dimensions':list(high-low),'UV':[layer.name for layer in obj.data.uv_layers],'customNormals':obj.data.has_custom_normals,'sourceMeshUntouched':source.data is not obj.data})
root=r'D:\projects\Pirate_BR\PirateGame\Art\Sources\CoastalEnvironment'
def leaf_material(name,diffuse,normal,alpha=None):
    material=bpy.data.materials.new(name)
    material.use_nodes=True
    nodes=material.node_tree.nodes
    links=material.node_tree.links
    shader=next(node for node in nodes if node.type=='BSDF_PRINCIPLED')
    shader.inputs['Roughness'].default_value=.8
    shader.inputs['Specular IOR Level'].default_value=.2
    image=nodes.new('ShaderNodeTexImage')
    image.image=bpy.data.images.load(diffuse,check_existing=True)
    image.image.pack()
    links.new(image.outputs['Color'],shader.inputs['Base Color'])
    if alpha:
        mask=nodes.new('ShaderNodeTexImage')
        mask.image=bpy.data.images.load(alpha,check_existing=True)
        mask.image.colorspace_settings.name='Non-Color'
        mask.image.pack()
        links.new(mask.outputs['Color'],shader.inputs['Alpha'])
    else:links.new(image.outputs['Alpha'],shader.inputs['Alpha'])
    normal_image=nodes.new('ShaderNodeTexImage')
    normal_image.image=bpy.data.images.load(normal,check_existing=True)
    normal_image.image.colorspace_settings.name='Non-Color'
    normal_image.image.pack()
    normal_node=nodes.new('ShaderNodeNormalMap')
    normal_node.inputs['Strength'].default_value=.8
    links.new(normal_image.outputs['Color'],normal_node.inputs['Color'])
    links.new(normal_node.outputs['Normal'],shader.inputs['Normal'])
    return material
palm_material=leaf_material('NativeCC0Palm',os.path.join(root,'yughues_palms','diffuse.tga'),os.path.join(root,'yughues_palms','normal.tga'))
fern_material=leaf_material('NativeCC0Fern',os.path.join(root,'fern_02','textures','fern_02_diff_2k.png'),os.path.join(root,'fern_02','textures','fern_02_nor_gl_2k.png'),os.path.join(root,'fern_02','textures','fern_02_alpha_2k.png'))
for key,path,material in [
    ('Palm',os.path.join(root,'yughues_palms','palm_straight.obj'),palm_material),
    ('PalmBent',os.path.join(root,'yughues_palms','palm_bend.obj'),palm_material),
    ('Fern',os.path.join(root,'fern_02','fern_02_2k.fbx'),fern_material)
]:
    before=set(bpy.data.objects)
    if path.endswith('.obj'):bpy.ops.wm.obj_import(filepath=path)
    else:bpy.ops.import_scene.fbx(filepath=path)
    imported=[obj for obj in bpy.data.objects if obj not in before and obj.type=='MESH']
    source=max(imported,key=lambda obj:len(obj.data.vertices))
    source.name='CC0Master_'+key
    matrix=source.matrix_world.copy()
    for vertex in source.data.vertices:vertex.co=matrix @ vertex.co
    source.matrix_world.identity()
    points=[v.co for v in source.data.vertices]
    low=Vector(tuple(min(v[i] for v in points) for i in range(3)))
    high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
    offset=Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z))
    for vertex in source.data.vertices:vertex.co-=offset
    source.data.update()
    source.data.materials.clear()
    source.data.materials.append(material)
    for polygon in source.data.polygons:polygon.material_index=0
    for collection in list(source.users_collection):collection.objects.unlink(source)
    masters.objects.link(source)
    source.hide_render=True
    source['ModuleKey']=key
    for obj in imported:
        if obj is not source:obj.hide_render=True
    report.append({'key':key,'nativeTris':sum(len(p.vertices)-2 for p in source.data.polygons),'dimensions':list(high-low),'UV':[layer.name for layer in source.data.uv_layers]})
path=r'D:\projects\Pirate_BR\output\CoastalEnvironment\prepared-native-module-stats.json'
json.dump(report,open(path,'w',encoding='utf-8'),indent=2)
bpy.context.window.scene=previous
result={'prepared':report,'scene':scene.name,'activeSceneRestored':previous.name}
