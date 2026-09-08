import bpy, math, os
from mathutils import Vector

root = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
output = os.path.join(root, 'Assets/Models/Ships/ClimbingRigging')
os.makedirs(output, exist_ok=True)
scene = bpy.data.scenes.new('ClimbingRiggingExport')
verts, faces = [], []

def tube(a, b, radius):
    a, b = Vector((a[0], -a[2], a[1])), Vector((b[0], -b[2], b[1]))
    direction = (b - a).normalized()
    u = direction.cross(Vector((0, 0, 1)))
    if u.length < .01:
        u = direction.cross(Vector((0, 1, 0)))
    u.normalize()
    v = direction.cross(u)
    start = len(verts)
    for point in (a, b):
        verts.extend(tuple(point + radius * (u * math.cos(i * math.tau / 6) + v * math.sin(i * math.tau / 6))) for i in range(6))
    for i in range(6):
        j = (i + 1) % 6
        faces.append((start + i, start + j, start + j + 6, start + i + 6))

for side in (-1, 1):
    for z in (-4.95, -4.3, -3.65):
        tube((side * 6, 4.3, z), (side * .8, 34.3, z), .045)
        tube((side * .8, 34.3, z), (0, 34.3, -2), .045)
    for step in range(75):
        y = 4.45 + step * .4
        x = side * (6 - 5.2 * (y - 4.3) / 30)
        tube((x, y, -4.95), (x, y, -3.65), .028)
        for z in (-4.95, -4.3, -3.65):
            tube((x, y - .045, z), (x, y + .045, z), .059)

mesh = bpy.data.meshes.new('ClimbingRigging')
mesh.from_pydata(verts, [], faces)
mesh.update()
obj = bpy.data.objects.new('ClimbingRigging', mesh)
scene.collection.objects.link(obj)
material = bpy.data.materials.new('ClimbingRope')
material.diffuse_color = (.30, .24, .14, 1)
mesh.materials.append(material)
previous = bpy.context.window.scene
try:
    bpy.context.window.scene = scene
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.data.libraries.write(os.path.join(os.path.dirname(__file__), 'ClimbingRigging.blend'), {scene})
    bpy.ops.export_scene.fbx(filepath=os.path.join(output, 'ClimbingRigging.fbx'), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False)
finally:
    bpy.context.window.scene = previous
    bpy.data.scenes.remove(scene)
result = {'vertices': len(verts), 'faces': len(faces), 'restored_scene': previous.name}
