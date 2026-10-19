import bpy, os, math
from mathutils import Vector

OUT=r'C:\Users\K\Documents\Codex\2026-10-19\new-chat\outputs'
FRAMES=r'C:\Users\K\Documents\Codex\2026-10-19\new-chat\work\KamikazeRunFrames'
os.makedirs(FRAMES,exist_ok=True)
scene=bpy.data.scenes['KamikazeAsset']
rig=bpy.data.objects['KamikazeRig']
camera=bpy.data.objects['KamikazePreviewCamera']
bpy.context.window.scene=scene
task=globals().get('TASK','cycle')
if task=='cycle':
    rig.animation_data.action=bpy.data.actions['KamikazeRun']
    camera.location=(5,-3,2.5)
    camera.rotation_euler=(Vector((0,0,1.15))-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.cycles.samples=16
    scene.render.resolution_x=512;scene.render.resolution_y=512
    for frame in range(1,19):
        scene.frame_set(frame)
        scene.render.filepath=os.path.join(FRAMES,'%02d.png'%frame)
        bpy.ops.render.render(write_still=True)
    rig.animation_data.action=bpy.data.actions['KamikazeIdle'];scene.frame_set(1)
    scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.cycles.samples=32
    result={'frames':18,'directory':FRAMES}
elif task=='sheet':
    old=bpy.data.scenes.get('KamikazeRunContactSheet')
    if old:bpy.data.scenes.remove(old)
    board=bpy.data.scenes.new('KamikazeRunContactSheet')
    bpy.context.window.scene=board
    board.render.engine='CYCLES';board.cycles.samples=1
    board.render.resolution_x=2400;board.render.resolution_y=1290;board.render.resolution_percentage=100
    board.render.image_settings.file_format='PNG'
    board.view_settings.view_transform='Standard'
    board.world=bpy.data.worlds.new('KamikazeBoardWorld');board.world.color=(.18,.18,.18)
    for i in range(18):
        frame=i+1;col=i%6;row=i//6
        x=col-2.5;y=1.04-row*1.08
        verts=[(x-.49,y-.49,0),(x+.49,y-.49,0),(x+.49,y+.49,0),(x-.49,y+.49,0)]
        me=bpy.data.meshes.new('CyclePanel');me.from_pydata(verts,[],[(0,1,2,3)]);me.update()
        uv=me.uv_layers.new()
        for j,coord in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[j].uv=coord
        ob=bpy.data.objects.new('Frame_%02d'%frame,me);board.collection.objects.link(ob)
        mat=bpy.data.materials.new('CycleFrame_%02d'%frame);mat.use_nodes=True;nodes=mat.node_tree.nodes;nodes.clear()
        tx=nodes.new('ShaderNodeTexImage');tx.image=bpy.data.images.load(os.path.join(FRAMES,'%02d.png'%frame),check_existing=True)
        emit=nodes.new('ShaderNodeEmission');mat.node_tree.links.new(tx.outputs['Color'],emit.inputs['Color'])
        output=nodes.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(emit.outputs[0],output.inputs['Surface']);ob.data.materials.append(mat)
        text=bpy.data.curves.new('FrameLabel','FONT');text.body='%02d   %.2fs'%(frame,(frame-1)/30);text.align_x='CENTER';text.size=.047;text.extrude=0
        label=bpy.data.objects.new('FrameLabel_%02d'%frame,text);board.collection.objects.link(label);label.location=(x,y-.54,.01)
        tm=bpy.data.materials.get('KamikazeContactLabel')
        if tm is None:
            tm=bpy.data.materials.new('KamikazeContactLabel');tm.use_nodes=True;n=tm.node_tree.nodes;n.clear();e=n.new('ShaderNodeEmission');e.inputs['Color'].default_value=(.9,.9,.9,1);out=n.new('ShaderNodeOutputMaterial');tm.node_tree.links.new(e.outputs[0],out.inputs[0])
        text.materials.append(tm)
    cdata=bpy.data.cameras.new('KamikazeBoardCamera');cam=bpy.data.objects.new('KamikazeBoardCamera',cdata);board.collection.objects.link(cam);cam.location=(0,-.02,10);cam.rotation_euler=(0,0,0);cdata.type='ORTHO';cdata.ortho_scale=6.3;board.camera=cam
    board.render.filepath=os.path.join(OUT,'KamikazeModelA_RunContactSheet.png')
    bpy.ops.render.render(write_still=True)
    result={'contact_sheet':board.render.filepath,'frames':18}
    bpy.context.window.scene=scene
