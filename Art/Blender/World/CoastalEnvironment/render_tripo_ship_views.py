import bpy
from pathlib import Path
from mathutils import Vector,Matrix

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
names=globals().get('TRIPO_SHIP_RENDER_NAMES',['Sea_Lagoon_Cave'])
views=globals().get('TRIPO_SHIP_VIEWS',None)
previous=bpy.context.window.scene
scene=bpy.data.scenes.get('TripoShipLevelPreview') or bpy.data.scenes.new('TripoShipLevelPreview')
scene.world=bpy.data.scenes['TripoCoastalCollection'].world.copy()
scene.render.engine='CYCLES'
scene.cycles.device='CPU'
scene.cycles.samples=16
scene.cycles.use_denoising=True
scene.render.resolution_x=1100
scene.render.resolution_y=820
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.view_settings.exposure=-.35
paths=[]

def source_matrix(obj):
    return obj.matrix_basis.copy() if obj.parent is None and not obj.constraints else obj.matrix_world.copy()

try:
    bpy.context.window.scene=scene
    for name in names:
        for obj in list(scene.objects):
            if obj.name.startswith('TripoShipPreview_'):
                bpy.data.objects.remove(obj,do_unlink=True)
        coll=bpy.data.collections['TripoSet_'+name]
        points=[source_matrix(obj)@v.co for obj in coll.objects if obj.type=='MESH' for v in obj.data.vertices]
        low=Vector(tuple(min(p[i] for p in points) for i in range(3)))
        high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
        size=high-low
        extent=max(size.x,size.y,size.z)
        factor=100/extent
        for source in coll.objects:
            if source.type!='MESH':continue
            obj=source.copy()
            obj.name='TripoShipPreview_'+source.name
            obj.parent=None
            scene.collection.objects.link(obj)
            obj.hide_viewport=False
            obj.hide_render=False
            obj.hide_set(False)
            obj.matrix_world=Matrix.Scale(factor,4)@source_matrix(source)
        mat=bpy.data.materials.get('TripoShipPreview_Ground') or bpy.data.materials.new('TripoShipPreview_Ground')
        mat.use_nodes=True
        shader=next(node for node in mat.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
        shader.inputs['Base Color'].default_value=(.24,.24,.24,1)
        shader.inputs['Roughness'].default_value=.88
        mesh=bpy.data.meshes.new('TripoShipPreview_Ground')
        mesh.from_pydata([(-1200,-1200,0),(1200,-1200,0),(1200,1200,0),(-1200,1200,0)],[],[(0,1,2,3)])
        ground=bpy.data.objects.new('TripoShipPreview_Ground',mesh)
        scene.collection.objects.link(ground)
        ground.data.materials.append(mat)
        center=Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,max(high.z,1)*.42))
        light_target=center*factor
        for position,energy,width in [((-70,-90,140),160000,55),((45,-100,12),65000,60),((20,95,125),130000,65)]:
            data=bpy.data.lights.new('TripoShipPreview_Light','AREA')
            data.energy=energy
            data.shape='DISK'
            data.size=width
            light=bpy.data.objects.new('TripoShipPreview_Light',data)
            scene.collection.objects.link(light)
            light.location=light_target+Vector(position)
            light.rotation_euler=(light_target-light.location).to_track_quat('-Z','Y').to_euler()
        data=bpy.data.cameras.new('TripoShipPreview_Camera')
        camera=bpy.data.objects.new('TripoShipPreview_Camera',data)
        scene.collection.objects.link(camera)
        data.type='PERSP'
        data.lens=36
        data.clip_end=3000
        scene.camera=camera
        model_views=views or (['outer','north','south','west','east','top'] if name=='Sea_Lagoon_Cave' else ['outer','passage','opposite','top'] if name=='SeaArch_Huge_A' else ['outer','opposite','top'])
        for view in model_views:
            data.type='PERSP'
            data.lens=36
            if view=='outer':
                point=Vector((center.x+extent*.12,low.y-extent*1.12,4))
                target=center
            elif view=='opposite':
                point=Vector((center.x-extent*.12,high.y+extent*1.12,4))
                target=center
            elif view=='passage':
                point=Vector((0,-32,4));target=Vector((0,0,72));data.lens=20
            elif view in {'north','south','west','east'}:
                point=Vector((0,0,4))
                target={'north':Vector((0,85,45)),'south':Vector((0,-85,43)),'west':Vector((-85,0,47)),'east':Vector((85,0,43))}[view]
                data.lens=22
            elif view=='top':
                data.type='ORTHO'
                data.ortho_scale=extent*1.3*factor
                point=Vector((center.x,center.y,extent*2));target=Vector((center.x,center.y,0))
            else:
                raise RuntimeError('Unknown ship-level view '+view)
            camera.location=point*factor
            camera.rotation_euler=(target*factor-camera.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(OUTPUT/(name+'-Tripo-rooted-ship-'+view+'.png'))
            bpy.context.view_layer.update()
            bpy.ops.render.render(write_still=True)
            paths.append(scene.render.filepath)
    result={'renders':paths,'sourceSceneUnchanged':True,'cameraWorldHeightMeters':4,'waterlineWorldZ':0}
finally:
    bpy.context.window.scene=previous
