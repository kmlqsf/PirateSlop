import bpy
import math
from pathlib import Path
from mathutils import Vector,Matrix

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
selection=globals().get('TRIPO_RENDER_NAMES',['Sea_Lagoon_Cave','Reef_Moai_A','Reef_Spires_A','Reef_Spires_B','Reef_Spires_C','Reef_ShallowField_A'])
previous=bpy.context.window.scene
scene=bpy.data.scenes.get('TripoCollectionPreview') or bpy.data.scenes.new('TripoCollectionPreview')
source_scene=bpy.data.scenes['TripoCoastalCollection']
scene.world=source_scene.world.copy()
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
    for name in selection:
        for obj in list(scene.objects):
            if obj.name.startswith('TripoPreview_'):
                bpy.data.objects.remove(obj,do_unlink=True)
        coll=bpy.data.collections['TripoSet_'+name]
        source_points=[obj.matrix_world@Vector(corner) for obj in coll.objects if obj.type=='MESH' for corner in obj.bound_box]
        low=Vector(tuple(min(point[i] for point in source_points) for i in range(3)))
        high=Vector(tuple(max(point[i] for point in source_points) for i in range(3)))
        size=high-low
        factor=100/max(size.x,size.y,size.z)
        for source in coll.objects:
            if source.type!='MESH':
                continue
            obj=source.copy()
            obj.name='TripoPreview_'+source.name
            obj.parent=None
            scene.collection.objects.link(obj)
            obj.hide_render=False
            obj.hide_set(False)
            obj.matrix_world=Matrix.Scale(factor,4)@source.matrix_world
        target=Vector(((low.x+high.x)*.5*factor,(low.y+high.y)*.5*factor,max(0,high.z)*.47*factor))
        data=bpy.data.cameras.new('TripoPreview_Camera')
        camera=bpy.data.objects.new('TripoPreview_Camera',data)
        scene.collection.objects.link(camera)
        data.type='ORTHO'
        data.ortho_scale=140
        camera.location=target+Vector((62,-190,95 if name=='Sea_Lagoon_Cave' else 63))
        camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.camera=camera
        for position,energy,width in [((-75,-115,155),180000,65),((110,-40,85),65000,90),((-20,110,140),130000,70)]:
            data=bpy.data.lights.new('TripoPreview_Light','AREA')
            data.energy=energy
            data.shape='DISK'
            data.size=width
            light=bpy.data.objects.new('TripoPreview_Light',data)
            scene.collection.objects.link(light)
            light.location=target+Vector(position)
            light.rotation_euler=(target-light.location).to_track_quat('-Z','Y').to_euler()
        mat=bpy.data.materials.get('TripoPreview_Ground') or bpy.data.materials.new('TripoPreview_Ground')
        mat.use_nodes=True
        shader=next(node for node in mat.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value=(.24,.24,.24,1)
        shader.inputs['Roughness'].default_value=.88
        ground_z=(low.z-.5)*factor if name=='Reef_ShallowField_A' else 0
        mesh=bpy.data.meshes.new('TripoPreview_Ground')
        mesh.from_pydata([(-1200,-1200,ground_z),(1200,-1200,ground_z),(1200,1200,ground_z),(-1200,1200,ground_z)],[],[(0,1,2,3)])
        ground=bpy.data.objects.new('TripoPreview_Ground',mesh)
        scene.collection.objects.link(ground)
        ground.data.materials.append(mat)
        scene.render.filepath=str(OUTPUT/(name+'-Tripo-collection.png'))
        bpy.ops.render.render(write_still=True)
        paths.append(scene.render.filepath)
        if name in {'Sea_Lagoon_Cave','Reef_Spires_C','Reef_ShallowField_A'}:
            camera.location=Vector((target.x,target.y,250))
            camera.rotation_euler=(0,0,0)
            camera.data.ortho_scale=130
            scene.render.filepath=str(OUTPUT/(name+'-Tripo-top.png'))
            bpy.ops.render.render(write_still=True)
            paths.append(scene.render.filepath)
        if name == 'Sea_Lagoon_Cave':
            camera.data.type='PERSP'
            camera.data.lens=22
            camera.location=Vector((0,-18,7))*factor
            camera.rotation_euler=(Vector((0,85,44))*factor-camera.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(OUTPUT/(name+'-Tripo-inside.png'))
            bpy.ops.render.render(write_still=True)
            paths.append(scene.render.filepath)
    result={'renders':paths,'sourceSceneUnchanged':True,'previousScene':previous.name}
finally:
    bpy.context.window.scene=previous
