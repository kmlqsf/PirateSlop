import bpy
from pathlib import Path
from mathutils import Vector,Matrix

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
previous=bpy.context.window.scene
scene=bpy.data.scenes.get('TripoCliffWallBBaseQC') or bpy.data.scenes.new('TripoCliffWallBBaseQC')
scene.world=bpy.data.scenes['TripoCoastalCollection'].world.copy()
scene.render.engine='CYCLES'
scene.cycles.samples=16
scene.cycles.use_denoising=True
scene.render.resolution_x=1000
scene.render.resolution_y=760
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.view_settings.exposure=-.35
paths=[]

try:
    bpy.context.window.scene=scene
    for obj in list(scene.objects):
        if obj.name.startswith('TripoCliffBQC_'):
            bpy.data.objects.remove(obj,do_unlink=True)
    coll=bpy.data.collections['TripoSet_CliffWallB']
    points=[obj.matrix_world@Vector(corner) for obj in coll.objects if obj.type=='MESH' for corner in obj.bound_box]
    low=Vector(tuple(min(point[i] for point in points) for i in range(3)))
    high=Vector(tuple(max(point[i] for point in points) for i in range(3)))
    factor=100/max(high-low)
    for source in coll.objects:
        if source.type!='MESH':
            continue
        obj=source.copy()
        obj.name='TripoCliffBQC_'+source.name
        obj.parent=None
        scene.collection.objects.link(obj)
        obj.hide_render=False
        obj.hide_viewport=False
        obj.hide_set(False)
        obj.matrix_world=Matrix.Scale(factor,4)@source.matrix_world
    material=bpy.data.materials.get('TripoCliffBQC_Ground') or bpy.data.materials.new('TripoCliffBQC_Ground')
    material.use_nodes=True
    shader=next(node for node in material.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value=(.24,.24,.24,1)
    shader.inputs['Roughness'].default_value=.88
    mesh=bpy.data.meshes.new('TripoCliffBQC_Ground')
    mesh.from_pydata([(-1200,-1200,0),(1200,-1200,0),(1200,1200,0),(-1200,1200,0)],[],[(0,1,2,3)])
    ground=bpy.data.objects.new('TripoCliffBQC_Ground',mesh)
    scene.collection.objects.link(ground)
    ground.data.materials.append(material)
    target=Vector(((low.x+high.x)*.5*factor,(low.y+high.y)*.5*factor,high.z*.45*factor))
    for position,energy,width in [((-75,-115,155),180000,65),((110,-100,45),90000,90),((-20,110,140),130000,70)]:
        light_data=bpy.data.lights.new('TripoCliffBQC_Light','AREA')
        light_data.energy=energy
        light_data.shape='DISK'
        light_data.size=width
        light=bpy.data.objects.new('TripoCliffBQC_Light',light_data)
        scene.collection.objects.link(light)
        light.location=target+Vector(position)
        light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
    camera_data=bpy.data.cameras.new('TripoCliffBQC_Camera')
    camera=bpy.data.objects.new('TripoCliffBQC_Camera',camera_data)
    scene.collection.objects.link(camera)
    camera_data.type='ORTHO'
    camera_data.ortho_scale=140
    scene.camera=camera
    bpy.context.view_layer.update()
    for label,offset in [('low',Vector((35,-230,0))),('side',Vector((230,-35,0)))]:
        camera.location=target+offset
        camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(OUTPUT/('CliffWallB-Tripo-base-'+label+'.png'))
        bpy.ops.render.render(write_still=True)
        paths.append(scene.render.filepath)
    result={'renders':paths,'sourceSceneUnchanged':True,'bodyMinimumZ':min((obj.matrix_world@v.co).z for obj in coll.objects if obj.name.endswith('_MainCliff') for v in obj.data.vertices),'waterlineZ':0}
finally:
    bpy.context.window.scene=previous
