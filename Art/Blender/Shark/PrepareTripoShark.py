from pathlib import Path
import math
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[3]
previous_scene = bpy.context.window.scene
scene = bpy.data.scenes['SharkTripoWork']
bpy.context.window.scene = scene
try:
    mesh = next(o for o in scene.objects if o.type == 'MESH')
    if mesh.name != 'Shark_Body':
        points = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
        low, high = min(p.y for p in points), max(p.y for p in points)
        scale = 5.2 / (high - low)
        for vertex, point in zip(mesh.data.vertices, points):
            vertex.co = Vector((point.x * scale, (point.y - (low + high) * .5) * scale, (point.z - .155) * scale))
        mesh.matrix_world.identity()
        mesh.name, mesh.data.name = 'Shark_Body', 'Shark_Tripo'
    source = root / 'Art/Blender/Shark/TripoSources'
    for material in mesh.data.materials:
        material.name = 'SharkTripoSkin'
        if material.use_nodes:
            for node in material.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image:
                    suffix = node.image.name.rsplit('_', 1)[-1].lower()
                    candidates = list(source.glob('Shark_' + suffix + '.*'))
                    if candidates:
                        node.image = bpy.data.images.load(str(candidates[0]), check_existing=True)
                        if suffix.split('.')[0] != 'basecolor':
                            node.image.colorspace_settings.name = 'Non-Color'
    arm = next((o for o in scene.objects if o.type == 'ARMATURE'), None)
    if arm is None:
        arm = bpy.data.objects.new('Shark_Armature', bpy.data.armatures.new('Shark_Armature'))
        scene.collection.objects.link(arm)
        bpy.ops.object.select_all(action='DESELECT')
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode='EDIT')
        chain = [('Body', -1.3), ('Spine', .15), ('Tail_01', .75), ('Tail_02', 1.3), ('Tail_03', 1.8), ('Tail_Fin', 2.2)]
        previous = None
        for index, (name, at) in enumerate(chain):
            bone = arm.data.edit_bones.new(name)
            bone.head = (0, at, 0)
            bone.tail = (0, chain[index + 1][1] if index + 1 < len(chain) else 2.65, 0)
            if previous:
                bone.parent, bone.use_connect = previous, True
            previous = bone
        jaw = arm.data.edit_bones.new('Jaw')
        jaw.head, jaw.tail = (0, -1.4, -.03), (0, -2.55, -.03)
        jaw.parent = arm.data.edit_bones['Body']
        for side, sign in [('Left', -1), ('Right', 1)]:
            fin = arm.data.edit_bones.new('Pectoral_' + side)
            fin.head, fin.tail = (sign * .38, -.25, -.53), (sign * 1.37, -.25, -.75)
            fin.parent = arm.data.edit_bones['Body']
        bpy.ops.object.mode_set(mode='OBJECT')
        mesh.parent = arm
        modifier = mesh.modifiers.new('SharkSkin', 'ARMATURE')
        modifier.object = arm
        groups = {b.name: mesh.vertex_groups.new(name=b.name) for b in arm.data.bones}
        def smooth(a, b, value):
            t = max(0, min(1, (value - a) / (b - a)))
            return t * t * (3 - 2 * t)
        for vertex in mesh.data.vertices:
            p = vertex.co
            fin = smooth(.43, .95, abs(p.x)) * (1 - smooth(.25, .8, abs(p.y + .25))) * (1 - smooth(-.35, -.12, p.z))
            jaw = (1 - smooth(-1.8, -1.2, p.y)) * (1 - smooth(-.07, .015, p.z)) * (1 - fin)
            base = 1 - jaw - fin
            weights = {'Jaw': jaw, 'Pectoral_Left' if p.x < 0 else 'Pectoral_Right': fin}
            if p.y <= chain[0][1]:
                weights['Body'] = base
            elif p.y >= chain[-1][1]:
                weights['Tail_Fin'] = base
            else:
                for index in range(len(chain) - 1):
                    if chain[index][1] <= p.y < chain[index + 1][1]:
                        blend = smooth(chain[index][1], chain[index + 1][1], p.y)
                        weights[chain[index][0]] = base * (1 - blend)
                        weights[chain[index + 1][0]] = base * blend
                        break
            for name, weight in weights.items():
                if weight > .0001:
                    groups[name].add([vertex.index], weight, 'REPLACE')
    arm.animation_data_create()
    scene.render.fps = 24
    for name, end in [('Shark_Swim', 40), ('Shark_Bite', 18)]:
        old = bpy.data.actions.get(name)
        if old:
            bpy.data.actions.remove(old)
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        arm.animation_data.action = action
        for frame in range(0, end + 1, 2):
            t = frame / end
            bite = math.sin(math.pi * min(1, max(0, (t - .08) / .84))) ** 2 if name == 'Shark_Bite' else 0
            for bone in arm.pose.bones:
                bone.rotation_mode = 'XYZ'
                bone.rotation_euler = (0, 0, 0)
            for index, bone_name in enumerate(['Body', 'Spine', 'Tail_01', 'Tail_02', 'Tail_03', 'Tail_Fin']):
                amplitude = [1.8, 3.2, 5.5, 7.5, 10, 12][index]
                arm.pose.bones[bone_name].rotation_euler.z = math.radians(amplitude) * math.sin(t * math.tau - index * .72)
            arm.pose.bones['Body'].rotation_euler.x = math.radians(-4) * bite
            arm.pose.bones['Jaw'].rotation_euler.x = math.radians(24) * bite
            for side, sign in [('Left', -1), ('Right', 1)]:
                arm.pose.bones['Pectoral_' + side].rotation_euler.y = sign * math.radians(3) * math.sin(t * math.tau + .5)
            for bone in arm.pose.bones:
                bone.keyframe_insert(data_path='rotation_euler', frame=frame)
    arm.animation_data.action = bpy.data.actions['Shark_Swim']
    scene.frame_start, scene.frame_end = 0, 40
    scene.frame_set(0)
    scene.unit_settings.system, scene.unit_settings.scale_length = 'METRIC', 1
    for bone in arm.pose.bones:
        bone.rotation_euler = (0, 0, 0)
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.wm.save_as_mainfile(filepath=str(root / 'Art/Blender/Shark/SharkTripoRig.blend'), copy=True)
    bpy.ops.export_scene.fbx(filepath=str(root / 'Assets/Models/Shark/SharkTripo.fbx'), use_selection=True, object_types={'MESH', 'ARMATURE'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, path_mode='STRIP', mesh_smooth_type='FACE', apply_scale_options='FBX_SCALE_UNITS')
    result = {'mesh': mesh.name, 'vertices': len(mesh.data.vertices), 'bones': len(arm.data.bones), 'actions': ['Shark_Swim', 'Shark_Bite'], 'length_m': 5.2}
finally:
    bpy.context.window.scene = previous_scene
