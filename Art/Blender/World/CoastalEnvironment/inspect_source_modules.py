import bpy
import bmesh
import json
import os
from mathutils import Vector
previous_scene=bpy.context.scene
scene=bpy.data.scenes.new('CoastalSourceModuleQC')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
root=r'D:\projects\Pirate_BR\PirateGame\Art\Sources\CoastalEnvironment'
report=[]
for asset in ['rock_face_02','coastal_cliff_02','rock_moss_set_01']:
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(root,asset,asset+'_2k.fbx'))
    imported=[obj for obj in bpy.data.objects if obj not in before]
    textures=os.path.join(root,asset,'textures')
    material=bpy.data.materials.new('SourcePBR_'+asset)
    material.use_nodes=True
    nodes=material.node_tree.nodes
    links=material.node_tree.links
    shader=next(node for node in nodes if node.type=='BSDF_PRINCIPLED')
    diffuse=nodes.new('ShaderNodeTexImage')
    diffuse.image=bpy.data.images.load(os.path.join(textures,asset+'_diff_2k.png'),check_existing=True)
    diffuse.image.pack()
    diffuse.label='Original UV Albedo'
    ao=nodes.new('ShaderNodeTexImage')
    ao.image=bpy.data.images.load(os.path.join(textures,asset+'_ao_2k.png'),check_existing=True)
    ao.image.colorspace_settings.name='Non-Color'
    ao.image.pack()
    multiply=nodes.new('ShaderNodeMixRGB')
    multiply.blend_type='MULTIPLY'
    multiply.inputs[0].default_value=.18
    links.new(diffuse.outputs['Color'],multiply.inputs[1])
    links.new(ao.outputs['Color'],multiply.inputs[2])
    links.new(multiply.outputs[0],shader.inputs['Base Color'])
    rough=nodes.new('ShaderNodeTexImage')
    rough.image=bpy.data.images.load(os.path.join(textures,asset+'_rough_2k.png'),check_existing=True)
    rough.image.colorspace_settings.name='Non-Color'
    rough.image.pack()
    links.new(rough.outputs['Color'],shader.inputs['Roughness'])
    normal=nodes.new('ShaderNodeTexImage')
    normal.image=bpy.data.images.load(os.path.join(textures,asset+'_nor_gl_2k.png'),check_existing=True)
    normal.image.colorspace_settings.name='Non-Color'
    normal.image.pack()
    normal_map=nodes.new('ShaderNodeNormalMap')
    normal_map.inputs['Strength'].default_value=1
    links.new(normal.outputs['Color'],normal_map.inputs['Color'])
    links.new(normal_map.outputs['Normal'],shader.inputs['Normal'])
    shader.inputs['Specular IOR Level'].default_value=.26
    for obj in imported:
        if obj.type!='MESH':continue
        source_name=obj.name
        obj.name='QC_'+asset+'_'+source_name
        obj['SourceAsset']=asset
        obj['SourceObject']=source_name
        obj.data.materials.clear()
        obj.data.materials.append(material)
        for polygon in obj.data.polygons:polygon.material_index=0
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        boundaries=sum(edge.is_boundary for edge in bm.edges)
        nonmanifold=sum(not edge.is_manifold for edge in bm.edges)
        bm.free()
        bounds=[obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
        dimensions=[max(v[i] for v in bounds)-min(v[i] for v in bounds) for i in range(3)]
        report.append({'asset':asset,'name':obj.name,'sourceName':source_name,'vertices':len(obj.data.vertices),'tris':sum(len(p.vertices)-2 for p in obj.data.polygons),'dimensions':dimensions,'boundaryEdges':boundaries,'nonManifoldEdges':nonmanifold,'uvLayers':[uv.name for uv in obj.data.uv_layers],'materials':[material.name],'location':list(obj.location),'rotation':list(obj.rotation_euler),'scale':list(obj.scale)})
path=r'D:\projects\Pirate_BR\output\CoastalEnvironment\source-module-stats.json'
json.dump(report,open(path,'w',encoding='utf-8'),indent=2)
save_path=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\CoastalSourceModuleQC.blend'
bpy.ops.wm.save_as_mainfile(filepath=save_path,copy=True,compress=True)
bpy.context.window.scene=previous_scene
result={'scene':scene.name,'sourceModules':report,'saved':save_path,'activeSceneRestored':previous_scene.name}
