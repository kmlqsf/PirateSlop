import bpy
import os
import sys
import math
import importlib
from mathutils import Matrix, Quaternion, Vector

folder = os.path.dirname(__file__)
if folder not in sys.path:
    sys.path.insert(0, folder)
import pose_studio as studio
importlib.reload(studio)

root = os.path.abspath(os.path.join(folder, '..', '..', '..'))
rig = next((o for o in bpy.context.scene.objects if o.get('animation_studio_rig')), None)
if rig is None:
    bpy.context.window.scene = bpy.data.scenes.new('SabreAnimationStudio')
    bpy.ops.import_scene.fbx(filepath=os.path.join(root, 'Assets', 'Models', 'Characters', 'Pirate', 'PirateCharacter.fbx'))
    rig = next(o for o in bpy.context.selected_objects if o.type == 'ARMATURE')
    rig['animation_studio_rig'] = True
    bpy.ops.import_scene.fbx(filepath=os.path.join(root, 'Assets', 'Models', 'PirateWeapons', 'SM_PirateCutlass.fbx'))
rig.animation_data_clear()
for bone in rig.pose.bones:
    bone.matrix_basis.identity()
studio.update()
mount = bpy.context.scene.objects.get('WeaponSocket_R')
if mount is None:
    mount = studio.socket(rig, 'R', 'WeaponSocket_R')
    prop = bpy.context.scene.objects['SM_PirateCutlass']
    prop.parent = mount
    prop.matrix_parent_inverse.identity()
    prop.matrix_basis = Matrix(((0, -1, 0, 0), (0, 0, 1, 0), (-1, 0, 0, 0), (0, 0, 0, 1)))

keys = [
    (0, (-.28, -.30, 1.27), (-.15, -.40, .90), (0, 0, -5)),
    (.12, (-.38, -.16, 1.42), (-.55, .10, .83), (0, 0, -12)),
    (.28, (-.40, -.08, 1.51), (-.60, .40, .70), (0, 0, -20)),
    (.30, (-.35, -.27, 1.48), (-.75, -.60, .28), (0, 0, -12)),
    (.39, (-.03, -.47, 1.30), (.10, -.98, .08), (0, 0, 8)),
    (.48, (.20, -.32, 1.12), (.85, -.40, -.35), (0, 0, 23)),
    (.60, (.22, -.21, 1.06), (.88, -.08, -.46), (0, 0, 25)),
    (.90, (-.28, -.30, 1.27), (-.15, -.40, .90), (0, 0, -5))
]

def pose(time):
    hand, blade, torso = studio.interpolate(keys, time)
    for name, weight in [('Spine', .35), ('Chest', .65), ('Head', -.55)]:
        bone = rig.pose.bones[name]
        base = bone.matrix.to_quaternion()
        studio.orient(rig, name, Quaternion((0, 0, 1), math.radians(torso.z * weight)) @ base)
    studio.reach(rig, 'UpperArm.R', 'Forearm.R', hand, (-.58, -.05, 1.08))
    studio.grip(rig, 'R', blade, (0, -1, -.25))
    studio.reach(rig, 'UpperArm.L', 'Forearm.L', (.25, -.23, 1.18), (.52, .03, .95))
    studio.grip(rig, 'L', (-1, 0, .1), (0, -.7, -.6), .45)

ready = studio.bake(rig, 'SabreReady', 1, lambda time: pose(0))
slash = studio.bake(rig, 'SabreSlash', .9, pose)
for action in [ready, slash]:
    studio.export(rig, action, os.path.join(root, 'Assets', 'Models', 'Characters', 'Pirate', action.name + '.fbx'), [mount])
bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(folder, 'PirateSabreAttack.blend'))
