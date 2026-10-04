import bpy, math, os
from mathutils import Vector, Matrix, Euler, Quaternion

rig=bpy.data.objects['ShipMonkeyRig']
mesh=bpy.data.objects['ShipMonkeyMesh']
scene=bpy.context.scene
REST={b.name:b.matrix_local.copy() for b in rig.data.bones}
PARENT={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
TAU=math.tau

def smooth(t):
    t=max(0,min(1,t));return t*t*(3-2*t)

def direct(name,a,b):
    a,b=Vector(a),Vector(b)
    rest=rig.data.bones[name]
    q=(rest.tail_local-rest.head_local).rotation_difference(b-a)
    mat=q.to_matrix().to_4x4()@REST[name]
    mat.translation=a
    return mat

def two_bone(a,c,l1,l2,pole):
    delta=c-a;dist=min(delta.length,l1+l2-.0001)
    axis=delta.normalized();c=a+axis*dist
    along=(l1*l1-l2*l2+dist*dist)/(2*dist)
    height=math.sqrt(max(0,l1*l1-along*along))
    bend=Vector(pole);bend-=axis*bend.dot(axis)
    bend.normalize()
    return a+axis*along+bend*height,c

def foot_cycle(phase,rail=False):
    duty=.62
    reach=.125 if not rail else .065
    if phase<duty:
        y=-reach+2*reach*phase/duty
        lift=0
        pitch=.30*smooth((phase/duty-.76)/.24)
    else:
        u=(phase-duty)/(1-duty)
        y=reach-2*reach*smooth(u)
        heel=1-smooth(u/.30)
        y-=.0133*heel
        lift=(.073 if not rail else .050)*math.sin(math.pi*u)**1.3+.024*heel
        pitch=.13*math.sin(math.pi*u)+.30*heel
    return y,lift,pitch

def pose(kind,t):
    walk=kind in ('Walk','RailWalk')
    climb=kind in ('ClimbUp','ClimbDown')
    rail=kind=='RailWalk'
    desired={}
    desired['Root']=REST['Root'].copy()
    def fk(name,angles=(0,0,0),offset=(0,0,0)):
        parent=PARENT[name]
        inherited=desired[parent]@REST[parent].inverted()@REST[name] if parent else REST[name].copy()
        rot=Euler(angles,'XYZ').to_matrix()@inherited.to_3x3()
        inherited=rot.to_4x4();inherited.translation=(desired[parent]@REST[parent].inverted()@REST[name]).translation+Vector(offset) if parent else REST[name].translation+Vector(offset)
        desired[name]=inherited
    if walk:
        fk('Pelvis',(.025, .035*math.sin(TAU*t), .045*math.sin(TAU*t)),(.009*math.sin(TAU*t),0,-.021+.0035*math.cos(2*TAU*t)))
        fk('Spine',(-.035,0,-.025*math.sin(TAU*t)))
        fk('Chest',(-.025,0,-.07*math.sin(TAU*t)))
    elif climb:
        fk('Pelvis',(.10, .045*math.sin(TAU*t),.03*math.sin(TAU*t)),(.008*math.sin(TAU*t),-.024,-.028+.008*math.cos(2*TAU*t)))
        fk('Spine',(.06,0,-.025*math.sin(TAU*t)))
        fk('Chest',(.05,0,-.035*math.sin(TAU*t)))
    else:
        fk('Pelvis',(0,.015*math.sin(TAU*t),.014*math.sin(TAU*t)),(.005*math.sin(TAU*t),0,-.006+.001*math.cos(2*TAU*t)))
        fk('Spine',(.009*math.sin(2*TAU*t),0,0))
        fk('Chest',(.01*math.sin(2*TAU*t-.6),0,.015*math.sin(TAU*t)))
    fk('Neck',(-.08 if climb else .012*math.sin(TAU*t),0,0))
    fk('Head',(-.13 if climb else -.025+.015*math.sin(TAU*t+.5),.015*math.sin(TAU*t),.05*math.sin(TAU*t) if kind=='Idle' else -.035*math.sin(TAU*t)))
    for side,sign in [('L',1),('R',-1)]:
        phase=(t+(0 if side=='L' else .5))%1
        pelvis_delta=desired['Pelvis']@REST['Pelvis'].inverted()
        hip=pelvis_delta@rig.data.bones['Thigh.'+side].head_local
        if walk:
            y,lift,pitch=foot_cycle(phase,rail)
            ankle=Vector((sign*(.04 if rail else .102),y,.056+lift))
            foot_rot=Euler((pitch,0,0)).to_matrix()
            if phase<.62:
                toe=Vector((ankle.x,ankle.y-.086,.024))
                ankle=toe+foot_rot@Vector((0,.086,.032))
        elif climb:
            duty=.67
            if phase<duty:
                y=-.072;lift=.12-.13*phase/duty
            else:
                u=(phase-duty)/(1-duty);y=-.072+.032*math.sin(math.pi*u);lift=-.01+.13*smooth(u)
            ankle=Vector((sign*.092,y,.075+lift))
            foot_rot=Euler((-.20,0,sign*.06)).to_matrix()
        else:
            ankle=Vector((sign*.103,.005,.056))
            foot_rot=Matrix.Identity(3)
        thigh=rig.data.bones['Thigh.'+side];shin=rig.data.bones['Shin.'+side]
        knee,ankle=two_bone(hip,ankle,thigh.length,shin.length,(sign*.17,-1,0))
        desired[thigh.name]=direct(thigh.name,hip,knee)
        desired[shin.name]=direct(shin.name,knee,ankle)
        foot=rig.data.bones['Foot.'+side]
        mat=foot_rot.to_4x4()@REST[foot.name];mat.translation=ankle
        desired[foot.name]=mat
        fk('Toes.'+side,(-pitch if walk and phase<.62 else 0,0,0))
        fk('Clavicle.'+side,(0,-sign*.025*math.sin(TAU*t),0))
        chest_delta=desired['Chest']@REST['Chest'].inverted()
        shoulder=chest_delta@rig.data.bones['UpperArm.'+side].head_local
        if climb:
            duty=.69
            if phase<duty:
                hand_z=.685-.17*phase/duty;hand_y=-.158
            else:
                u=(phase-duty)/(1-duty);hand_z=.515+.17*smooth(u);hand_y=-.158+.06*math.sin(math.pi*u)
            wrist=Vector((sign*.128,hand_y,hand_z))
            palm_dir=Vector((sign*.002,-.02,.065))
            elbow_pole=(sign*1,.35,-.15)
        elif rail:
            wrist=Vector((sign*.295,-.028+.018*math.sin(TAU*phase),.407+.01*math.sin(TAU*phase)))
            palm_dir=Vector((sign*.05,-.02,-.04))
            elbow_pole=(sign*.25,.6,-.3)
        else:
            swing=.051*math.cos(TAU*phase) if walk else .006*math.sin(TAU*t)
            wrist=Vector((sign*.142,-.027+swing,.278+.004*math.sin(TAU*phase)))
            palm_dir=Vector((sign*.012,-.028,-.059))
            elbow_pole=(sign*.15,.6,-.15)
        upper=rig.data.bones['UpperArm.'+side];lower=rig.data.bones['Forearm.'+side]
        elbow,wrist=two_bone(shoulder,wrist,upper.length,lower.length,elbow_pole)
        desired[upper.name]=direct(upper.name,shoulder,elbow)
        desired[lower.name]=direct(lower.name,elbow,wrist)
        desired['Hand.'+side]=direct('Hand.'+side,wrist,wrist+palm_dir)
        fk('Fingers.'+side,(-.15 if not climb else -.36,0,0))
        fk('Thumb.'+side,(.12 if not climb else .3,0,sign*.09))
    for i in range(10):
        amplitude=.023 if kind=='Idle' else .045 if not climb else .030
        fk('Tail%02d'%i,(amplitude*math.sin(TAU*t-i*.43),.025*math.sin(TAU*t-i*.4),amplitude*.75*math.sin(TAU*t-i*.45+1.0)))
    fk('Sash',(.025*math.sin(TAU*t-1),.028*math.sin(TAU*t-.7),.018*math.cos(TAU*t)))
    fk('Bandana',(.035*math.sin(TAU*t-1.2),.024*math.sin(TAU*t-.8),.018*math.cos(TAU*t)))
    for name in ('Eye.L','Eye.R'):
        if name in rig.data.bones: fk(name)
    for bone in rig.data.bones:
        pb=rig.pose.bones[bone.name];pb.rotation_mode='QUATERNION'
        parent=PARENT[bone.name]
        if parent:
            pb.matrix_basis=bone.convert_local_to_pose(desired[bone.name],REST[bone.name],parent_matrix=desired[parent],parent_matrix_local=REST[parent],invert=True)
        else:
            pb.matrix_basis=bone.convert_local_to_pose(desired[bone.name],REST[bone.name],invert=True)
    bpy.context.view_layer.update()

durations={'Idle':90,'Walk':30,'RailWalk':36,'ClimbUp':36,'ClimbDown':36}
for kind,length in durations.items():
    name='Legacy'+kind
    old=bpy.data.actions.get(name)
    if old:
        continue
    action=bpy.data.actions.new(name);action.use_fake_user=True
    rig.animation_data_create();rig.animation_data.action=action
    for f in range(length+1):
        scene.frame_set(f+1)
        phase=f/length
        pose(kind,(1-phase)%1 if kind=='ClimbDown' else phase%1)
        for pb in rig.pose.bones:
            pb.keyframe_insert('location',frame=f+1,group=pb.name)
            pb.keyframe_insert('rotation_quaternion',frame=f+1,group=pb.name)
            pb.keyframe_insert('scale',frame=f+1,group=pb.name)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for key in fc.keyframe_points: key.interpolation='LINEAR'
rig.animation_data.action=bpy.data.actions['LegacyWalk']
scene.frame_start=1;scene.frame_end=30;scene.frame_set(1)
result={'clips':list(durations),'frames':durations}
