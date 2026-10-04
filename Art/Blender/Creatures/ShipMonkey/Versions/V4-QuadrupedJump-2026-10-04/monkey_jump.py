import bpy, math
from pathlib import Path
exec(Path(r'C:\Users\K\Project\Art\Blender\Creatures\ShipMonkey\monkey_animation.py').read_text().split('\ndurations=')[0])
rig.animation_data.action = None
pose('Idle', 0)
base={b.name:rig.pose.bones[b.name].matrix.copy() for b in rig.data.bones}
def jump_pose(kind,t):
    compression=smooth(t) if kind=='JumpStart' else 2.5*math.sin(math.pi*t)*math.exp(-3*t) if kind=='JumpLand' else 0
    tuck=1 if kind=='JumpAir' else 0
    offset=Vector((0,0,-.035*compression))
    desired={name:mat.copy() for name,mat in base.items()}
    for name,mat in desired.items():
        if name!='Root':mat.translation+=offset
    for side,sign in [('L',1),('R',-1)]:
        hip=desired['Thigh.'+side].translation
        ankle=base['Shin.'+side]@Vector((0,rig.data.bones['Shin.'+side].length,0))
        ankle+=Vector((0,-.035*tuck,(.12+.006*math.sin(math.tau*t))*tuck))
        knee,ankle=two_bone(hip,ankle,rig.data.bones['Thigh.'+side].length,rig.data.bones['Shin.'+side].length,(sign*.15,-1,0))
        desired['Thigh.'+side]=direct('Thigh.'+side,hip,knee)
        desired['Shin.'+side]=direct('Shin.'+side,knee,ankle)
        desired['Foot.'+side].translation=ankle
        shoulder=desired['UpperArm.'+side].translation
        wrist=base['Hand.'+side].translation+Vector((0,.035*tuck,(.085+.004*math.sin(math.tau*t))*tuck))
        elbow,wrist=two_bone(shoulder,wrist,rig.data.bones['UpperArm.'+side].length,rig.data.bones['Forearm.'+side].length,(sign*.3,.75,-.2))
        desired['UpperArm.'+side]=direct('UpperArm.'+side,shoulder,elbow)
        desired['Forearm.'+side]=direct('Forearm.'+side,elbow,wrist)
        desired['Hand.'+side].translation=wrist
    for b in rig.data.bones:
        if b.name.startswith(('Toes.','Fingers.','Thumb.','Eye.')) or b.name in ('Bandana','Sash') or b.name.startswith('Tail'):
            parent=PARENT[b.name]
            desired[b.name]=desired[parent]@base[parent].inverted()@base[b.name]
            if b.name.startswith('Tail'):
                mat=desired[b.name];location=mat.translation.copy()
                mat=mat@Matrix.Rotation(.02*tuck*math.sin(math.tau*t-int(b.name[4:])*.4),4,'X');mat.translation=location
                desired[b.name]=mat
        parent=PARENT[b.name]
        kw={'parent_matrix':desired[parent],'parent_matrix_local':REST[parent]} if parent else {}
        pb=rig.pose.bones[b.name];pb.rotation_mode='QUATERNION'
        pb.matrix_basis=b.convert_local_to_pose(desired[b.name],REST[b.name],invert=True,**kw)
for kind,frames in {'JumpStart':6,'JumpAir':12,'JumpLand':8}.items():
    old=bpy.data.actions.get(kind)
    if old:bpy.data.actions.remove(old)
    a=bpy.data.actions.new(kind);a.use_fake_user=True;rig.animation_data.action=a
    for f in range(frames+1):
        scene.frame_set(f+1);jump_pose(kind,f/frames)
        for pb in rig.pose.bones:
            for channel in ('location','rotation_quaternion','scale'):pb.keyframe_insert(channel,frame=f+1,group=pb.name)
    for layer in a.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for key in fc.keyframe_points:key.interpolation='LINEAR'
result={'added':['JumpStart','JumpAir','JumpLand'],'total':len(bpy.data.actions)}
