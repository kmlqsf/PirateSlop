import bpy
import math
import json
import os
from mathutils import Vector
previous_scene=bpy.context.scene
scene=bpy.data.scenes['CoastalSourceModuleQC']
bpy.context.window.scene=scene
for obj in scene.objects:obj.hide_render=True
selected=[
    ('QC_rock_face_02_rock_face_02','A  Rock Face 02',(-7,7),7),
    ('QC_coastal_cliff_02_coastal_cliff_02_LOD3','B  Coastal Cliff 02', (7,7),12),
    ('QC_rock_moss_set_01_rock_moss_set_01_rock01','C  Moss Rock 01',(-7,-7),6),
    ('QC_rock_moss_set_01_rock_moss_set_01_rock03','D  Moss Rock 03',(7,-7),6)
]
display=[]
for name,label,position,size in selected:
    source=bpy.data.objects[name]
    obj=source.copy()
    obj.name='QCDisplay_'+label.replace(' ','_')
    scene.collection.objects.link(obj)
    obj.hide_render=False
    bounds=[obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    low=Vector(tuple(min(p[i] for p in bounds) for i in range(3)))
    high=Vector(tuple(max(p[i] for p in bounds) for i in range(3)))
    factor=size/max(high-low)
    obj.scale*=factor
    bounds=[obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    low=Vector(tuple(min(p[i] for p in bounds) for i in range(3)))
    high=Vector(tuple(max(p[i] for p in bounds) for i in range(3)))
    obj.location+=Vector((position[0],position[1],0))-Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z))
    display.append((obj,label))
world=bpy.data.worlds.new('SourceQCNeutralWorld')
world.use_nodes=True
background=next(node for node in world.node_tree.nodes if node.type=='BACKGROUND')
background.inputs['Color'].default_value=(.38,.42,.46,1)
background.inputs['Strength'].default_value=.5
scene.world=world
camera_data=bpy.data.cameras.new('SourceQCCamera')
camera=bpy.data.objects.new('SourceQCCamera',camera_data)
scene.collection.objects.link(camera)
camera_data.type='ORTHO'
camera_data.ortho_scale=31.0
camera_data.clip_end=1000
target=Vector((0,0,2.4))
camera.location=target+Vector((0,-34,24))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
scene.camera=camera
for position,energy,size in [((-18,-16,25),9500,15),((20,-5,14),4200,20),((0,20,22),6500,15)]:
    data=bpy.data.lights.new('SourceQCLight','AREA')
    data.energy=energy
    data.shape='DISK'
    data.size=size
    light=bpy.data.objects.new('SourceQCLight',data)
    scene.collection.objects.link(light)
    light.location=position
    light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
ground_mesh=bpy.data.meshes.new('SourceQCGround')
ground_mesh.from_pydata([(-40,-40,-.07),(40,-40,-.07),(40,40,-.07),(-40,40,-.07)],[],[(0,1,2,3)])
ground=bpy.data.objects.new('SourceQCGround',ground_mesh)
scene.collection.objects.link(ground)
ground_material=bpy.data.materials.new('SourceQCGround')
ground_material.use_nodes=True
shader=next(node for node in ground_material.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
shader.inputs['Base Color'].default_value=(.25,.27,.29,1)
shader.inputs['Roughness'].default_value=.94
ground.data.materials.append(ground_material)
label_material=bpy.data.materials.new('SourceQCLabels')
label_material.use_nodes=True
nodes=label_material.node_tree.nodes
nodes.clear()
output=nodes.new('ShaderNodeOutputMaterial')
emission=nodes.new('ShaderNodeEmission')
emission.inputs['Color'].default_value=(.8,.86,.9,1)
emission.inputs['Strength'].default_value=1
label_material.node_tree.links.new(emission.outputs[0],output.inputs['Surface'])
rotation=camera.rotation_euler.to_quaternion()
inverse=rotation.inverted()
bpy.context.view_layer.update()
for obj,label in display:
    bounds=[inverse @ (obj.matrix_world @ Vector(corner)-target) for corner in obj.bound_box]
    u=(min(point.x for point in bounds)+max(point.x for point in bounds))*.5
    v=min(point.y for point in bounds)-.8
    text_data=bpy.data.curves.new('SourceQCLabel','FONT')
    text_data.body=label
    text_data.align_x='CENTER'
    text_data.size=.52
    text=bpy.data.objects.new('SourceQCLabel',text_data)
    scene.collection.objects.link(text)
    text.location=target+rotation @ Vector((u,v,12))
    text.rotation_euler=camera.rotation_euler
    text.data.materials.append(label_material)
scene.render.engine='CYCLES'
scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.render.resolution_x=1800
scene.render.resolution_y=1500
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.view_settings.exposure=0
path=r'D:\projects\Pirate_BR\output\CoastalEnvironment\CC0-source-rocks-contact.png'
scene.render.filepath=path
bpy.ops.render.render(write_still=True)
save_path=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\CoastalSourceModuleQC.blend'
bpy.ops.wm.save_as_mainfile(filepath=save_path,copy=True,compress=True)
bpy.context.window.scene=previous_scene
result={'contact':path,'saved':save_path,'displayModules':[{'name':obj.name,'source':obj['SourceAsset'],'scale':list(obj.scale),'tris':sum(len(p.vertices)-2 for p in obj.data.polygons)} for obj,label in display],'activeSceneRestored':previous_scene.name}
