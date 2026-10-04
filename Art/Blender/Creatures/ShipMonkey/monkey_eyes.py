import bpy

rig = bpy.data.objects['ShipMonkeyRig']
mesh = bpy.data.objects['ShipMonkeyMesh']
head = mesh.vertex_groups['Head']
restored = 0
eye_ids = {g.index for g in mesh.vertex_groups if g.name in ('Eye.L','Eye.R')}
for vertex in mesh.data.vertices:
    if not any(g.group in eye_ids and g.weight > 0 for g in vertex.groups):
        continue
    for entry in list(vertex.groups):
        mesh.vertex_groups[entry.group].remove([vertex.index])
    head.add([vertex.index], 1, 'REPLACE')
    restored += 1
for name in ('Eye.L','Eye.R'):
    group = mesh.vertex_groups.get(name)
    if group: mesh.vertex_groups.remove(group)
    bone = rig.data.bones.get(name)
    if bone: bone.use_deform = False
result = {'restored_vertices': restored, 'eye_skin_groups': 0, 'gaze': 'head only'}
