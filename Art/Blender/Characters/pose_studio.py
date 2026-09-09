import bpy
import math
from mathutils import Vector, Matrix, Quaternion


def update():
    bpy.context.view_layer.update()


def orient(rig, name, rotation):
    bone = rig.pose.bones[name]
    matrix = rotation.to_matrix().to_4x4()
    matrix.translation = bone.head
    bone.matrix = matrix
    update()


def aim(rig, name, direction):
    rest = rig.data.bones[name].matrix_local.to_quaternion()
    orient(rig, name, (rest @ Vector((0, 1, 0))).rotation_difference(Vector(direction).normalized()) @ rest)


def reach(rig, upper, lower, point, pole):
    a, b = rig.pose.bones[upper], rig.pose.bones[lower]
    start = a.head.copy()
    delta = Vector(point) - start
    length = max(abs(a.length - b.length) + .0001, min(delta.length, a.length + b.length - .0001))
    axis = delta.normalized()
    bend = Vector(pole) - start
    bend = (bend - axis * bend.dot(axis)).normalized()
    along = (a.length * a.length - b.length * b.length + length * length) / (2 * length)
    joint = start + axis * along + bend * math.sqrt(max(0, a.length * a.length - along * along))
    aim(rig, upper, joint - start)
    aim(rig, lower, Vector(point) - b.head)


def hand_basis(rig, side):
    bone = rig.data.bones['Hand.' + side]
    distal = (bone.tail_local - bone.head_local).normalized()
    across = rig.data.bones['Index1.' + side].head_local - rig.data.bones['Little1.' + side].head_local
    across = (across - distal * across.dot(distal)).normalized()
    return Matrix((across, distal, across.cross(distal))).transposed()


def grip(rig, side, blade_axis, finger_direction, amount=1):
    across = Vector(blade_axis).normalized()
    distal = Vector(finger_direction)
    distal = (distal - across * distal.dot(across)).normalized()
    normal = across.cross(distal)
    delta = Matrix((across, distal, normal)).transposed() @ hand_basis(rig, side).transposed()
    orient(rig, 'Hand.' + side, (delta @ rig.data.bones['Hand.' + side].matrix_local.to_3x3()).to_quaternion())
    for digit in ['Index', 'Middle', 'Ring', 'Little', 'Thumb']:
        for j in range(1, 3 if digit == 'Thumb' else 4):
            bone = rig.pose.bones[digit + str(j) + '.' + side]
            base = delta @ bone.bone.matrix_local.to_3x3()
            axis = (base @ Vector((0, 1, 0))).cross(normal).normalized()
            angle = (.55 if digit == 'Thumb' else [0, 1.05, 1.25, .75][j]) * amount
            bone.rotation_quaternion = Quaternion(base.inverted() @ axis, angle)
    update()


def socket(rig, side, name):
    bone = rig.data.bones['Hand.' + side]
    basis = hand_basis(rig, side)
    across, distal, normal = (basis.col[i] for i in range(3))
    matrix = Matrix((across.cross(distal), across, distal)).transposed().to_4x4()
    matrix.translation = bone.head_local + distal * .075 + normal * .015
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = rig
    obj.parent_type = 'BONE'
    obj.parent_bone = bone.name
    update()
    obj.matrix_world = rig.matrix_world @ matrix
    return obj


def interpolate(keys, time):
    for left, right in zip(keys, keys[1:]):
        if time <= right[0]:
            t = max(0, (time - left[0]) / (right[0] - left[0]))
            t = t * t * (3 - 2 * t)
            return [Vector(a).lerp(Vector(b), t) for a, b in zip(left[1:], right[1:])]
    return [Vector(v) for v in keys[-1][1:]]


def bake(rig, name, duration, pose, fps=60):
    action = bpy.data.actions.get(name) or bpy.data.actions.new(name)
    for layer in list(action.layers):
        action.layers.remove(layer)
    action.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = action
    scene = bpy.context.scene
    scene.render.fps = fps
    scene.render.fps_base = 1
    previous = {}
    for frame in range(round(duration * fps) + 1):
        scene.frame_set(frame + 1)
        for bone in rig.pose.bones:
            bone.rotation_mode = 'QUATERNION'
            bone.matrix_basis.identity()
        update()
        pose(frame / fps)
        for bone in rig.pose.bones:
            q = bone.rotation_quaternion.copy()
            if bone.name in previous and q.dot(previous[bone.name]) < 0:
                q.negate()
            bone.rotation_quaternion = q
            previous[bone.name] = q.copy()
            for channel in ['rotation_quaternion', 'location']:
                bone.keyframe_insert(data_path=channel, frame=frame + 1, group=bone.name)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = 'LINEAR'
    return action


def export(rig, action, path, sockets=()):
    scene = bpy.context.scene
    rig.animation_data.action = action
    scene.frame_start, scene.frame_end = map(int, action.frame_range)
    selected = list(bpy.context.selected_objects)
    active = bpy.context.view_layer.objects.active
    try:
        for obj in selected:
            obj.select_set(False)
        for obj in scene.objects:
            if obj == rig or obj in sockets or obj.type == 'MESH' and any(m.type == 'ARMATURE' and m.object == rig for m in obj.modifiers):
                obj.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'}, add_leaf_bones=False, axis_forward='-Z', axis_up='Y', bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0, bake_anim_step=1)
    finally:
        for obj in bpy.context.selected_objects:
            obj.select_set(False)
        for obj in selected:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = active
