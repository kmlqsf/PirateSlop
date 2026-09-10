import bpy
import math
import os
import sys
import importlib
import bmesh
from mathutils import Vector, Matrix, Quaternion

folder = os.path.dirname(__file__)
root = os.path.abspath(os.path.join(folder, '..', '..', '..'))
if folder not in sys.path:
    sys.path.insert(0, folder)
import pose_studio as studio
importlib.reload(studio)
scene = bpy.context.scene
rig = next(o for o in scene.objects if o.type == 'ARMATURE')
model = next(o for o in scene.objects if o.type == 'MESH' and o.name.startswith('SniperMusket'))
model.hide_set(False)
for obj in scene.objects:
    if obj.name.startswith('PirateDoubleBarrel'):
        obj.hide_set(True)
anchor = scene.objects.get('ActionProp')
if anchor is None:
    anchor = bpy.data.objects.new('ActionProp', None)
    scene.collection.objects.link(anchor)
model.parent = anchor
model.matrix_parent_inverse.identity()
model.matrix_basis.identity()
rod = scene.objects.get('ActionRamrod')
if rod is None:
    mesh = model.data
    adjacency = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)
    indices = {800}
    pending = [800]
    while pending:
        for index in adjacency[pending.pop()]:
            if index not in indices:
                indices.add(index)
                pending.append(index)
    rod_mesh = mesh.copy()
    for data, keep in [(rod_mesh, True), (mesh, False)]:
        bm = bmesh.new()
        bm.from_mesh(data)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if (v.index in indices) != keep], context='VERTS')
        bm.to_mesh(data)
        bm.free()
    for vertex in rod_mesh.vertices:
        vertex.co -= Vector((.84, 0, 0))
    rod = bpy.data.objects.new('ActionRamrod', rod_mesh)
    scene.collection.objects.link(rod)
    rod.parent = anchor
    rod.location = (.84, 0, 0)
gun_rotation = Quaternion((0, 0, 1), -math.pi / 2)
rod.hide_set(False)
right_offset = Vector((-.45, -.055, -.08))
left_offset = Vector((-.21, .08, -.07))
cartridge = scene.objects.get('ActionCartridge')
if cartridge is None:
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=.009, depth=.06)
    cartridge = bpy.context.object
    cartridge.name = 'ActionCartridge'
    cartridge.parent = anchor
    paper = bpy.data.materials.get('CartridgePaper') or bpy.data.materials.new('CartridgePaper')
    paper.diffuse_color = (.65,.48,.26,1)
    cartridge.data.materials.append(paper)

def pose(time, mode):
    blend = .0 if mode == 'Ready' else 1.0
    position = Vector((-.18, -.75, 1.46)).lerp(Vector((-.22, -.65, 1.20)), 1-blend)
    tilt = 0 if mode != 'Ready' else -.08
    breathing = math.sin(math.pi*time) if mode in ['Ready','Aim'] else 0
    position.z += breathing * (.003 if mode == 'Ready' else .0015)
    rotation = Quaternion((1, 0, 0), tilt) @ gun_rotation
    right = None
    left = None
    amount = .85
    rod.location = (.84,0,0)
    rod.rotation_mode = 'QUATERNION'
    rod.rotation_quaternion = Quaternion()
    if mode == 'Fire':
        recoil = studio.interpolate([(0, (0,0)), (.035, (1,0)), (.12, (.55,0)), (.38, (0,0))], time)[0].x
        position.y += recoil * .045
        rotation = Quaternion((1, 0, 0), -recoil*.05) @ rotation
    if mode == 'Reload':
        values = studio.interpolate([
            (0, (-.18,-.75,1.46), (0,0,0), (-.235,-.30,1.38), (-.10,-.54,1.39)),
            (.35, (-.25,-.43,.82), (-90,0,0), (-.30,-.30,1.15), (-.17,-.315,1.04)),
            (.60, (-.25,-.43,.82), (-90,0,0), (-.30,-.14,1.02), (-.17,-.315,1.04)),
            (.95, (-.25,-.43,.82), (-90,0,0), (-.13,-.24,1.59), (-.17,-.315,1.04)),
            (1.15, (-.25,-.43,.82), (-90,0,0), (-.15,-.28,1.60), (-.17,-.315,1.04)),
            (1.50, (-.25,-.43,.82), (-90,0,0), (-.32,-.315,1.79), (-.17,-.315,1.04)),
            (1.85, (-.25,-.43,.82), (-90,0,0), (-.32,-.315,1.79), (-.17,-.315,1.04)),
            (2.10, (-.25,-.43,.82), (-90,0,0), (-.39,-.29,1.65), (-.17,-.315,1.04)),
            (2.40, (-.22,-.60,1.2), (-12,0,0), (-.30,-.30,1.18), (-.10,-.40,1.14)),
            (2.70, (-.22,-.60,1.2), (-12,0,0), (-.275,-.19,1.15), (-.10,-.40,1.14)),
            (3.2, (-.18,-.75,1.46), (0,0,0), (-.235,-.30,1.38), (-.10,-.54,1.39))
        ], time)
        position, angles, right, left = values
        rotation = Quaternion((1,0,0), math.radians(angles.x)) @ gun_rotation
        amount = .65
    anchor.location = position
    anchor.rotation_mode = 'QUATERNION'
    anchor.rotation_quaternion = rotation
    frame = round(time*60)+1
    for channel in ['location', 'rotation_quaternion']:
        anchor.keyframe_insert(data_path=channel, frame=frame)
    reload_blend = min(max(time/.35,0), max((3.2-time)/.5,0),1) if mode == 'Reload' else 0
    for name, weight in [('Spine',.45),('Chest',.55),('Head',-.65)]:
        turn = Quaternion((0,0,1), math.radians(-30*weight*(1-reload_blend)))
        lean = Quaternion((1,0,0), math.radians((8*reload_blend+.25*breathing)*weight))
        studio.orient(rig, name, lean @ turn @ rig.pose.bones[name].matrix.to_quaternion())
    if right is None:
        right = position + rotation @ right_offset
        left = position + rotation @ left_offset
    if mode == 'Reload':
        engage = min(max((time-.7)/.25,0), max((2.65-time)/.25,0), 1)
        left = left.lerp(Vector((-.17,-.315,1.04)), engage)
        return_grip = max(0, min(1, (time-2.4)/.3))
        right = right.lerp(position + rotation @ right_offset, return_grip)
        left = left.lerp(position + rotation @ left_offset, return_grip)
    for channel in ['location', 'rotation_quaternion']:
        rod.keyframe_insert(data_path=channel, frame=frame)
    studio.update()
    cartridge.rotation_mode = 'QUATERNION'
    cartridge.matrix_world = Matrix.Translation(right + Vector((.07,0,-.015)))
    visible = mode == 'Reload' and .6 <= time <= 1.9
    cartridge.scale = (1,1,1) if visible else (0,0,0)
    for channel in ['location','rotation_quaternion','scale']:
        cartridge.keyframe_insert(data_path=channel, frame=frame)
    for side, target, pole in [('R',right,(-.60,.05,1.17)),('L',left,(.40,-.12,1.03))]:
        studio.reach(rig, 'UpperArm.'+side, 'Forearm.'+side, target, pole)
        across = Vector((0,0,1)) if side=='R' else Vector((0,1,0))
        if side=='L' and mode=='Reload':
            across = across.lerp(Vector((0,0,1)), engage)
        studio.grip(rig, side, across, (1,0,0) if side=='R' else (-1,0,0), amount)

actions = {}
for mode, duration in [('Ready',2),('Aim',2),('Fire',.38),('Reload',3.2)]:
    anchor.animation_data_clear()
    rod.animation_data_clear()
    cartridge.animation_data_clear()
    action = studio.bake(rig, 'Musket'+mode, duration, lambda t: pose(t, mode))
    prop_action = anchor.animation_data.action
    prop_action.name = action.name+'_Prop'
    prop_action.use_fake_user = True
    action['prop_action'] = prop_action.name
    rod.animation_data.action.name = action.name+'_Ramrod'
    rod.animation_data.action.use_fake_user = True
    action['rod_action'] = rod.animation_data.action.name
    cartridge.animation_data.action.name = action.name+'_Cartridge'
    cartridge.animation_data.action.use_fake_user = True
    action['cartridge_action'] = cartridge.animation_data.action.name
    actions[mode] = action
rig.animation_data.action = actions['Aim']
anchor.animation_data.action = bpy.data.actions[actions['Aim']['prop_action']]
rod.animation_data.action = bpy.data.actions[actions['Aim']['rod_action']]
cartridge.animation_data.action = bpy.data.actions[actions['Aim']['cartridge_action']]
scene.frame_start = 1
scene.frame_end = 120
scene.frame_set(1)
for action in actions.values():
    anchor.animation_data.action = bpy.data.actions[action['prop_action']]
    rod.animation_data.action = bpy.data.actions[action['rod_action']]
    cartridge.animation_data.action = bpy.data.actions[action['cartridge_action']]
    studio.export(rig, action, os.path.join(root, 'Assets', 'Models', 'Characters', 'Pirate', action.name+'.fbx'), [anchor, rod, cartridge, model])
rig.animation_data.action = actions['Aim']
anchor.animation_data.action = bpy.data.actions[actions['Aim']['prop_action']]
rod.animation_data.action = bpy.data.actions[actions['Aim']['rod_action']]
cartridge.animation_data.action = bpy.data.actions[actions['Aim']['cartridge_action']]
scene.frame_end = 120
scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(folder, 'PirateFirearms_Actions.blend'))

