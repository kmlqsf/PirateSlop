import bpy, math
from pathlib import Path
exec(Path(r'C:\Users\K\Project\Art\Blender\Creatures\ShipMonkey\monkey_animation.py').read_text(encoding='utf-8').split('\ndurations=')[0])
walk_samples=[sample_action('LegacyWalk',f+1) for f in range(31)]

def activity_pose(kind,t):
    base=walk_samples[round(t*30)] if kind=='CarryWalk' else standing
    desired={name:mat.copy() for name,mat in base.items()}
    crouch=.10*math.sin(math.pi*t)**2 if kind=='Pickup' else 0
    for name,mat in desired.items():
        if name!='Root':mat.translation+=Vector((0,0,-crouch))
    for side,sign in [('L',1),('R',-1)]:
        if crouch>0:
            hip=desired['Pelvis']@REST['Pelvis'].inverted()@rig.data.bones['Thigh.'+side].head_local
            ankle=base['Foot.'+side].translation.copy()
            knee,ankle=two_bone(hip,ankle,rig.data.bones['Thigh.'+side].length,rig.data.bones['Shin.'+side].length,(sign*.1,-1,0))
            desired['Thigh.'+side]=direct('Thigh.'+side,hip,knee)
            desired['Shin.'+side]=direct('Shin.'+side,knee,ankle)
            desired['Foot.'+side].translation=ankle
        shoulder=desired['Chest']@REST['Chest'].inverted()@rig.data.bones['UpperArm.'+side].head_local
        pulse=math.sin(math.tau*t+(0 if side=='R' else math.pi))
        if kind=='Pickup':
            k=math.sin(math.pi*t)**2
            wrist=base['Hand.'+side].translation.lerp(Vector((sign*.095,-.22,.10)),k)
        elif kind=='Work':wrist=Vector((sign*.10,-.235+.045*pulse,.435+.02*pulse))
        elif kind.startswith('Fishing'):
            reel=kind=='FishingReel'
            wave=.015*pulse if reel else .003*pulse
            wrist=Vector((sign*.105,-.23 if side=='R' else -.16,.435+wave if side=='R' else .39+wave))
            if kind=='FishingCast':wrist+=Vector((0,.08*math.cos(math.pi*t),.04*math.sin(math.pi*t)))
        else:wrist=Vector((sign*.10,-.215,.425+.003*math.sin(math.tau*t)))
        elbow,wrist=two_bone(shoulder,wrist,rig.data.bones['UpperArm.'+side].length,rig.data.bones['Forearm.'+side].length,(sign*.25,.8,-.2))
        desired['UpperArm.'+side]=direct('UpperArm.'+side,shoulder,elbow)
        desired['Forearm.'+side]=direct('Forearm.'+side,elbow,wrist)
        desired['Hand.'+side]=direct('Hand.'+side,wrist,wrist+Vector((sign*.006,-.060,-.012)))
    for b in rig.data.bones:
        if b.name.startswith(('Toes.','Fingers.','Thumb.','Eye.')) or b.name in ('Bandana','Sash') or b.name.startswith('Tail'):
            parent=PARENT[b.name]
            desired[b.name]=desired[parent]@base[parent].inverted()@base[b.name]
    return desired
for kind,frames in {'Pickup':21,'CarryIdle':90,'CarryWalk':30,'FishingCast':20,'FishingWait':90,'FishingReel':75,'Work':45}.items():
    bake_action(kind,frames,lambda t,k=kind:activity_pose(k,t))
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
result={'added':['Pickup','CarryIdle','CarryWalk','FishingCast','FishingWait','FishingReel','Work'],'total':len(bpy.data.actions)}
