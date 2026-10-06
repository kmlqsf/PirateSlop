import bpy
from mathutils import Vector
previous=bpy.data.scenes['Scene']
scene=bpy.data.scenes.get('TripoRockContact') or bpy.data.scenes.new('TripoRockContact')
for obj in list(scene.objects):
    if obj.name.startswith('TripoContact_') or obj.name.startswith('TripoContactCamera') or obj.name.startswith('TripoContactLight') or obj.name.startswith('TripoContactGround') or obj.name.startswith('TripoContactLabel'):
        bpy.data.objects.remove(obj,do_unlink=True)
try:
    bpy.context.window.scene=scene
    display=[]
    for source_id,label,x,y in [
        ('01_RockMass_A','01 Mass A',-8,6),
        ('04_RockMonolith_A','04 Monolith A',0,6),
        ('05_RockMonolith_B','05 Monolith B',8,6),
        ('07_RockTerrace_A','07 Terrace A',-8,-6),
        ('09_RockWedge_A','09 Wedge A',0,-6),
        ('11_RockElongated_A','11 Elongated A',8,-6)
    ]:
        source=bpy.data.objects['TripoMaster_'+source_id]
        obj=source.copy()
        obj.name='TripoContact_'+source_id
        scene.collection.objects.link(obj)
        obj.hide_render=False
        obj.hide_set(False)
        factor=5.8/max(obj.dimensions)
        obj.scale=(factor,factor,factor)
        points=[vertex.co for vertex in obj.data.vertices]
        low=Vector(tuple(min(v[i] for v in points) for i in range(3)))
        high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
        obj.location=(x,y,-low.z*factor)
        display.append((obj,label))
    world=bpy.data.worlds.new('TripoContactWorld')
    world.use_nodes=True
    next(node for node in world.node_tree.nodes if node.type=='BACKGROUND').inputs['Color'].default_value=(.38,.42,.46,1)
    next(node for node in world.node_tree.nodes if node.type=='BACKGROUND').inputs['Strength'].default_value=.5
    scene.world=world
    camera_data=bpy.data.cameras.new('TripoContactCamera')
    camera=bpy.data.objects.new('TripoContactCamera',camera_data)
    scene.collection.objects.link(camera)
    camera_data.type='ORTHO'
    camera_data.ortho_scale=27
    target=Vector((0,0,2.4))
    camera.location=target+Vector((3,-32,23))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.camera=camera
    for position,energy,size in [((-18,-16,25),8500,14),((20,-5,14),4000,18),((0,20,22),6000,15)]:
        data=bpy.data.lights.new('TripoContactLight','AREA')
        data.energy=energy
        data.shape='DISK'
        data.size=size
        obj=bpy.data.objects.new('TripoContactLight',data)
        scene.collection.objects.link(obj)
        obj.location=position
        obj.rotation_euler=(target-obj.location).to_track_quat('-Z','Y').to_euler()
    material=bpy.data.materials.new('TripoContactGround')
    material.use_nodes=True
    shader=next(node for node in material.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value=(.25,.27,.29,1)
    shader.inputs['Roughness'].default_value=.92
    mesh=bpy.data.meshes.new('TripoContactGround')
    mesh.from_pydata([(-40,-40,-.05),(40,-40,-.05),(40,40,-.05),(-40,40,-.05)],[],[(0,1,2,3)])
    ground=bpy.data.objects.new('TripoContactGround',mesh)
    scene.collection.objects.link(ground)
    ground.data.materials.append(material)
    bpy.context.view_layer.update()
    for obj,label in display:
        data=bpy.data.curves.new('TripoContactLabel','FONT')
        data.body=label
        data.align_x='CENTER'
        data.size=.36
        text=bpy.data.objects.new('TripoContactLabel',data)
        scene.collection.objects.link(text)
        text.location=(obj.location.x,obj.location.y-3.4,.04)
        text.rotation_euler=(0,0,0)
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1600
    scene.render.resolution_y=1250
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    scene.render.filepath=r'D:\projects\Pirate_BR\output\CoastalEnvironment\tripo-source-native-contact.png'
    bpy.ops.render.render(write_still=True)
    bpy.context.window.scene=previous
    result={'render':scene.render.filepath,'activeSceneRestored':previous.name,'sourceMaterialsUntouched':True}
    
finally:
    bpy.context.window.scene=previous
