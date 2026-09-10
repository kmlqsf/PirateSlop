import bpy
import math
import os
import sys
from mathutils import Vector, Quaternion, Matrix

folder = os.path.dirname(__file__)
root = os.path.abspath(os.path.join(folder, '..', '..', '..'))
if folder not in sys.path:
    sys.path.insert(0, folder)
import pose_studio as studio
scene = bpy.context.scene
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
anchor = scene.objects['ActionProp']
cartridge = scene.objects['ActionCartridge']
model = next(o for o in scene.objects if o.type == 'MESH' and o.name.startswith('PirateDoubleBarrel'))
model.parent = anchor
model.matrix_parent_inverse.identity()
model.matrix_basis.identity()
model.hide_set(False)
for obj in scene.objects:
    if obj.name.startswith('SniperMusket') or obj.name == 'ActionRamrod':
        obj.hide_set(True)
gun_rotation = Quaternion((0,0,1), -math.pi/2)

def pose(time, mode):
    ready = mode == 'Ready'
    breath = math.sin(math.pi*time) if mode in ['Ready','Aim'] else 0
    position = Vector((-.18,-.52,1.46)) if not ready else Vector((-.20,-.48,1.22))
    position.z += breath*.002
    rotation = Quaternion((1,0,0), -.12 if ready else 0) @ gun_rotation
    right = None
    reload_blend = 0
    if mode == 'Fire':
        kick = studio.interpolate([(0,(0,0)),(.04,(1,0)),(.14,(.4,0)),(.38,(0,0))],time)[0].x
        position.y += kick*.075
        rotation = Quaternion((1,0,0),-kick*.12) @ rotation
    if mode == 'Reload':
        reload_blend = min(max(time/.4,0),max((3.6-time)/.55,0),1)
        values = studio.interpolate([
            (0,(-.18,-.52,1.46),(0,0,0),(-.235,-.20,1.38)),
            (.4,(-.18,-.39,1.02),(-90,0,0),(-.28,-.20,1.14)),
            (.65,(-.18,-.39,1.02),(-90,0,0),(-.31,-.13,1.01)),
            (1.05,(-.18,-.39,1.02),(-90,0,0),(-.299,-.271,1.64)),
            (1.35,(-.18,-.39,1.02),(-90,0,0),(-.299,-.271,1.56)),
            (1.6,(-.18,-.39,1.02),(-90,0,0),(-.31,-.13,1.01)),
            (2.05,(-.18,-.39,1.02),(-90,0,0),(-.201,-.271,1.64)),
            (2.35,(-.18,-.39,1.02),(-90,0,0),(-.201,-.271,1.56)),
            (2.65,(-.18,-.39,1.02),(-90,0,0),(-.30,-.20,1.20)),
            (3.05,(-.20,-.48,1.22),(-12,0,0),(-.255,-.16,1.14)),
            (3.6,(-.18,-.52,1.46),(0,0,0),(-.235,-.20,1.38))
        ],time)
        position,angles,right = values
        rotation = Quaternion((1,0,0),math.radians(angles.x)) @ gun_rotation
    anchor.location = position
    anchor.rotation_mode = 'QUATERNION'
    anchor.rotation_quaternion = rotation
    for name,weight in [('Spine',.45),('Chest',.55),('Head',-.65)]:
        studio.orient(rig,name,Quaternion((0,0,1),math.radians(-30*weight*(1-reload_blend))) @ rig.pose.bones[name].matrix.to_quaternion())
    grip = position + rotation @ Vector((-.32,-.055,-.08))
    left = position + rotation @ Vector((-.10,.06,-.02))
    if right is None:
        right = grip
    else:
        left = left.lerp(Vector((-.10,-.30,1.22)),reload_blend)
        right = right.lerp(grip,max(0,min(1,(time-2.65)/.4)))
    studio.update()
    cartridge.rotation_mode = 'QUATERNION'
    cartridge.matrix_world = Matrix.Translation(right+Vector((.07,0,-.015)))
    visible = mode == 'Reload' and (.66 <= time <= 1.35 or 1.65 <= time <= 2.35)
    cartridge.scale = (1,1,1) if visible else (0,0,0)
    frame = round(time*60)+1
    for obj in [anchor,cartridge]:
        for channel in ['location','rotation_quaternion','scale']:
            obj.keyframe_insert(data_path=channel,frame=frame)
    for side,target,pole in [('R',right,(-.60,.05,1.12)),('L',left,(.4,-.12,1.03))]:
        studio.reach(rig,'UpperArm.'+side,'Forearm.'+side,target,pole)
        across = Vector((0,0,1)) if side == 'R' else Vector((0,1,0)).lerp(Vector((0,0,1)),reload_blend)
        studio.grip(rig,side,across,(1,0,0) if side == 'R' else (-1,0,0),.78)

actions = {}
for mode,duration in [('Ready',2),('Aim',2),('Fire',.38),('Reload',3.6)]:
    anchor.animation_data_clear()
    cartridge.animation_data_clear()
    action = studio.bake(rig,'Shotgun'+mode,duration,lambda t:pose(t,mode))
    for obj,key,suffix in [(anchor,'prop_action','_Prop'),(cartridge,'cartridge_action','_Cartridge')]:
        obj.animation_data.action.name = action.name+suffix
        obj.animation_data.action.use_fake_user = True
        action[key] = obj.animation_data.action.name
    actions[mode] = action
    studio.export(rig,action,os.path.join(root,'Assets','Models','Characters','Pirate',action.name+'.fbx'),[anchor,cartridge,model])
rig.animation_data.action = actions['Aim']
anchor.animation_data.action = bpy.data.actions[actions['Aim']['prop_action']]
cartridge.animation_data.action = bpy.data.actions[actions['Aim']['cartridge_action']]
scene.frame_start = 1
scene.frame_end = 120
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(folder,'PirateFirearms_Actions.blend'))
