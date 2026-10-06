import bpy
import os
from mathutils import Vector
previous=bpy.context.scene
scene=bpy.data.scenes['CoastalModularArch']
bpy.context.window.scene=scene
bpy.context.view_layer.update()
visible=list(bpy.data.collections['SeaArchNativeRocks'].objects)+list(bpy.data.collections['SeaArchNativePlants'].objects)
points=[obj.matrix_world @ Vector(corner) for obj in visible for corner in obj.bound_box]
low=Vector(tuple(min(point[i] for point in points) for i in range(3)))
high=Vector(tuple(max(point[i] for point in points) for i in range(3)))
center=(low+high)*.5
extent=max(high-low)
scene.world=bpy.data.worlds['SourceQCNeutralWorld']
temporary=[]
camera_data=bpy.data.cameras.new('NativeArchReviewCamera')
camera=bpy.data.objects.new('NativeArchReviewCamera',camera_data)
scene.collection.objects.link(camera)
temporary.append(camera)
camera_data.type='ORTHO'
camera_data.clip_end=2000
scene.camera=camera
for relative,energy,size in [((-.85,-1.1,1.35),25,.6),((1,-.2,.6),8,1),((.3,.8,1.2),15,.8)]:
    data=bpy.data.lights.new('NativeArchReviewLight','AREA')
    data.energy=energy*extent*extent
    data.shape='DISK'
    data.size=size*extent
    obj=bpy.data.objects.new('NativeArchReviewLight',data)
    scene.collection.objects.link(obj)
    obj.location=center+Vector(relative)*extent
    obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
    temporary.append(obj)
mesh=bpy.data.meshes.new('NativeArchReviewGround')
mesh.from_pydata([(-260,-220,0),(260,-220,0),(260,220,0),(-260,220,0)],[],[(0,1,2,3)])
ground=bpy.data.objects.new('NativeArchReviewGround',mesh)
scene.collection.objects.link(ground)
ground.data.materials.append(bpy.data.materials['SourceQCGround'])
temporary.append(ground)
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
scene.render.resolution_x=1600
scene.render.resolution_y=1100
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.view_settings.exposure=0
views=[('overall',center,Vector((.42,-1.75,.48)),extent*1.17),('detail',Vector((-66,-12,77)),Vector((.18,-1.4,.38)),68),('passage',Vector((0,3,43)),Vector((0,-1.5,.10)),144)]
paths=[]
for name,target,relative,scale in views:
    if name not in globals().get('NATIVE_ARCH_VIEWS',['overall']):continue
    camera.location=target+relative*extent
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.ortho_scale=scale
    path=os.path.join(r'D:\projects\Pirate_BR\output\CoastalEnvironment','SeaArch-Huge-A-native-'+name+'.png')
    scene.render.filepath=path
    bpy.ops.render.render(write_still=True)
    paths.append(path)
for obj in temporary:bpy.data.objects.remove(obj,do_unlink=True)
scene.camera=None
save_path=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\SeaArch_Huge_A_NativeModules.blend'
bpy.ops.wm.save_as_mainfile(filepath=save_path,copy=True,compress=True)
bpy.context.window.scene=previous
result={'renders':paths,'saved':save_path,'activeSceneRestored':previous.name}
