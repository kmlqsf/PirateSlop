import bpy, math
from mathutils import Vector

rig = bpy.data.objects['ShipMonkeyRig']
mesh = bpy.data.objects['ShipMonkeyMesh']
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
for side, sign in [('L', 1), ('R', -1)]:
    name = 'Eye.' + side
    bone = rig.data.edit_bones.get(name) or rig.data.edit_bones.new(name)
    bone.head = (sign * .052, -.165, .647)
    bone.tail = (sign * .052, -.195, .647)
    bone.parent = rig.data.edit_bones['Head']
    bone.align_roll(Vector((0, 0, 1)))
bpy.ops.object.mode_set(mode='OBJECT')
head = mesh.vertex_groups['Head']
counts = {}
for side, sign in [('L', 1), ('R', -1)]:
    group = mesh.vertex_groups.get('Eye.' + side) or mesh.vertex_groups.new(name='Eye.' + side)
    count = 0
    for vertex in mesh.data.vertices:
        x, y, z = vertex.co
        radius = math.sqrt(((x - sign * .052) / .025) ** 2 + ((z - .647) / .032) ** 2)
        if radius >= 1 or y >= -.167:
            continue
        fade = max(0, min(1, (1 - radius) / .35))
        fade = fade * fade * (3 - 2 * fade)
        depth = max(0, min(1, (-y - .167) / .012))
        weight = fade * depth
        if weight < .001:
            continue
        for entry in list(vertex.groups):
            mesh.vertex_groups[entry.group].remove([vertex.index])
        group.add([vertex.index], weight, 'REPLACE')
        head.add([vertex.index], 1 - weight, 'REPLACE')
        count += 1
    counts[side] = count
result = {'eye_vertices': counts, 'bones': len(rig.data.bones)}
