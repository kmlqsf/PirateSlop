import bpy, math
from pathlib import Path
exec(Path(r'C:\Users\K\Project\Art\Blender\Creatures\ShipMonkey\monkey_animation.py').read_text(encoding='utf-8').split('\ndurations=')[0])

def repair_pose(t):
    pulse=math.sin(math.pi*min(1,t/.7))**2
    desired={name:mat.copy() for name,mat in standing.items()}
    for side,sign in [('L',1),('R',-1)]:
        shoulder=desired['Chest']@REST['Chest'].inverted()@rig.data.bones['UpperArm.'+side].head_local
        wrist=Vector((sign*.11,-.20+.12*pulse if side=='R' else -.16,.40+.18*pulse if side=='R' else .36))
        elbow,wrist=two_bone(shoulder,wrist,rig.data.bones['UpperArm.'+side].length,rig.data.bones['Forearm.'+side].length,(sign*.25,.8,-.2))
        desired['UpperArm.'+side]=direct('UpperArm.'+side,shoulder,elbow)
        desired['Forearm.'+side]=direct('Forearm.'+side,elbow,wrist)
        desired['Hand.'+side]=direct('Hand.'+side,wrist,wrist+Vector((sign*.006,-.060,-.012)))
    for b in rig.data.bones:
        if b.name.startswith(('Fingers.','Thumb.')):
            parent=PARENT[b.name]
            desired[b.name]=desired[parent]@standing[parent].inverted()@standing[b.name]
    return desired

bake_action('Repair',15,repair_pose)
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_set(1)
result={'added':'Repair','total':len(bpy.data.actions)}
