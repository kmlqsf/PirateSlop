import bpy
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
previous=bpy.data.scenes.get('Scene') or bpy.context.window.scene
scene=bpy.data.scenes['TripoArchScene']
try:
    bpy.context.window.scene=scene
    background=next(node for node in scene.world.node_tree.nodes if node.type=='BACKGROUND')
    background.inputs['Color'].default_value=(.4,.4,.4,1)
    ground=bpy.data.objects['TripoArch_Ground']
    ground_shader=next(node for node in ground.data.materials[0].node_tree.nodes if node.type=='BSDF_PRINCIPLED')
    ground_shader.inputs['Base Color'].default_value=(.24,.24,.24,1)
    for name,target,position,scale in [
        ('TripoArch_CameraOverall',(0,0,60),(112,-355,158),240),
        ('TripoArch_CameraFront',(0,0,60),(0,-355,137),240),
        ('TripoArch_CameraDetail',(-51,-10,89),(14,-170,132),110),
        ('TripoArch_CameraPassage',(0,0,55),(40,-300,80),216)
    ]:
        camera=bpy.data.objects[name]
        camera.location=position
        camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
        camera.data.ortho_scale=scale
    paths=[]
    for camera,suffix in [
        ('TripoArch_CameraOverall','overall'),
        ('TripoArch_CameraDetail','detail'),
        ('TripoArch_CameraPassage','passage'),
        ('TripoArch_CameraFront','front')
    ]:
        if suffix not in globals().get('TRIPO_ARCH_VIEWS',('overall','detail','passage','front')):
            continue
        scene.camera=bpy.data.objects[camera]
        scene.render.filepath=str(OUTPUT/('SeaArch_Huge_A_Tripo-'+suffix+'.png'))
        bpy.ops.render.render(write_still=True)
        paths.append(scene.render.filepath)
    scene.camera=bpy.data.objects['TripoArch_CameraOverall']
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'SeaArch_Huge_A_Tripo.blend'),copy=True,compress=True)
    result={'renders':paths,'blend':str(SOURCE/'SeaArch_Huge_A_Tripo.blend'),'originalSceneRestored':previous.name}
finally:
    bpy.context.window.scene=previous
