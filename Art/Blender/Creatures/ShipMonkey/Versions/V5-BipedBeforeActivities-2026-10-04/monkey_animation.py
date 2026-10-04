import bpy, math
from pathlib import Path
from mathutils import Vector, Matrix
exec(Path(r'C:\Users\K\Project\Art\Blender\Creatures\ShipMonkey\monkey_legacy.py').read_text(encoding='utf-8').split('\ndurations=')[0])

def sample_action(name,frame=1):
    rig.animation_data.action=bpy.data.actions[name]
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    return {b.name:rig.pose.bones[b.name].matrix.copy() for b in rig.data.bones}

def apply_pose(desired):
    for b in rig.data.bones:
        parent=PARENT[b.name]
        kw={'parent_matrix':desired[parent],'parent_matrix_local':REST[parent]} if parent else {}
        pb=rig.pose.bones[b.name];pb.rotation_mode='QUATERNION'
        pb.matrix_basis=b.convert_local_to_pose(desired[b.name],REST[b.name],invert=True,**kw)
    bpy.context.view_layer.update()

def bake_action(name,frames,generate):
    old=bpy.data.actions.get(name)
    if old:bpy.data.actions.remove(old)
    action=bpy.data.actions.new(name);action.use_fake_user=True
    rig.animation_data.action=action
    for f in range(frames+1):
        scene.frame_set(f+1);apply_pose(generate(f/frames))
        for pb in rig.pose.bones:
            for channel in ('location','rotation_quaternion','scale'):pb.keyframe_insert(channel,frame=f+1,group=pb.name)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for key in fc.keyframe_points:key.interpolation='LINEAR'

standing=sample_action('LegacyIdle')
balancing=sample_action('LegacyRailWalk')
seated=sample_action('Sit')
seated_perch=sample_action('SitPerch')

def transition_pose(start,end,t):
    t=smooth(t)
    if t<=0:return {name:mat.copy() for name,mat in start.items()}
    if t>=1:return {name:mat.copy() for name,mat in end.items()}
    desired={name:start[name].lerp(end[name],t) for name in start}
    for side,sign in [('L',1),('R',-1)]:
        hip=desired['Pelvis']@REST['Pelvis'].inverted()@rig.data.bones['Thigh.'+side].head_local
        ankle=desired['Foot.'+side].translation.copy()
        knee,ankle=two_bone(hip,ankle,rig.data.bones['Thigh.'+side].length,rig.data.bones['Shin.'+side].length,desired['Shin.'+side].translation-hip)
        desired['Thigh.'+side]=direct('Thigh.'+side,hip,knee)
        desired['Shin.'+side]=direct('Shin.'+side,knee,ankle)
        desired['Foot.'+side].translation=ankle
        shoulder=desired['Chest']@REST['Chest'].inverted()@rig.data.bones['UpperArm.'+side].head_local
        wrist=desired['Hand.'+side].translation.copy()
        elbow,wrist=two_bone(shoulder,wrist,rig.data.bones['UpperArm.'+side].length,rig.data.bones['Forearm.'+side].length,desired['Forearm.'+side].translation-shoulder)
        desired['UpperArm.'+side]=direct('UpperArm.'+side,shoulder,elbow)
        desired['Forearm.'+side]=direct('Forearm.'+side,elbow,wrist)
        desired['Hand.'+side].translation=wrist
    for b in rig.data.bones:
        if b.name.startswith(('Toes.','Fingers.','Thumb.','Eye.')):
            parent=PARENT[b.name]
            local_start=start[parent].inverted()@start[b.name]
            local_end=end[parent].inverted()@end[b.name]
            desired[b.name]=desired[parent]@local_start.lerp(local_end,t)
    return desired

def rail_idle(t):
    desired={name:mat.copy() for name,mat in balancing.items()}
    for name,mat in desired.items():
        if name!='Root' and not name.startswith(('Thigh.','Shin.','Foot.','Toes.')):
            mat.translation+=Vector((.002*math.sin(math.tau*t),0,.001*math.sin(math.tau*t)))
    return desired

durations={'IdleRail':90,'SitDown':21,'StandUp':21,'SitDownPerch':21,'StandUpPerch':21}
for active,original in {'Idle':'LegacyIdle','Walk':'LegacyWalk','RailWalk':'LegacyRailWalk','ClimbUp':'LegacyClimbUp','ClimbDown':'LegacyClimbDown','Run':'LegacyWalk'}.items():
    old=bpy.data.actions.get(active)
    if old:bpy.data.actions.remove(old)
    action=bpy.data.actions[original].copy();action.name=active;action.use_fake_user=True
bake_action('IdleRail',90,rail_idle)
for perch in (False,True):
    suffix='Perch' if perch else ''
    start=balancing if perch else standing
    end=seated_perch if perch else seated
    bake_action('SitDown'+suffix,21,lambda t,a=start,b=end:transition_pose(a,b,t))
    bake_action('StandUp'+suffix,21,lambda t,a=start,b=end:transition_pose(a,b,1-t))
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
result={'locomotion':'Biped','legacy_preserved':5,'actions':len(bpy.data.actions)}
