import bpy
import math
import os
import shutil
import numpy as np
from mathutils import Vector, Matrix

project = r'D:\projects\Pirate_BR\PirateGame'
art = os.path.join(project, 'Art', 'Blender', 'Firearms')
exports = os.path.join(project, 'Assets', 'Models', 'Firearms')
previous = bpy.context.window.scene
scene = bpy.data.scenes.new('AssembledFirearms')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
sources = {}
for key in ['Pistol', 'Musket', 'Shotgun', 'Trigger', 'Hammer', 'Frizzen']:
    source = bpy.data.objects.get(key + '_Source_0')
    if source is None:
        source_scene = bpy.data.scenes.get('FirearmAssemblySources') or bpy.data.scenes.new('FirearmAssemblySources')
        bpy.context.window.scene = source_scene
        folder = os.path.join(art, 'Sources', key)
        source_path = next(os.path.join(folder, f) for f in os.listdir(folder) if f.lower().endswith('.fbx'))
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=source_path)
        source = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
        source.name = key + '_Source_0'
        bpy.context.window.scene = scene
    sources[key] = source
    material = source.data.materials[0]
    material.name = 'Firearm_' + key
    folder = os.path.join(exports, 'Textures')
    os.makedirs(folder, exist_ok=True)
    images = [n.image for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
    base = next(i for i in images if 'basecolor' in i.name)
    normal = next(i for i in images if 'normal' in i.name)
    metal = next(i for i in images if 'metallic' in i.name)
    rough = next(i for i in images if 'roughness' in i.name)
    shutil.copyfile(bpy.path.abspath(base.filepath), os.path.join(folder, key + 'BaseColor.jpg'))
    shutil.copyfile(bpy.path.abspath(normal.filepath), os.path.join(folder, key + 'Normal.png'))
    count = metal.size[0] * metal.size[1]
    m = np.empty(count * 4, dtype=np.float32)
    r = np.empty(count * 4, dtype=np.float32)
    metal.pixels.foreach_get(m)
    rough.pixels.foreach_get(r)
    pixels = np.zeros((count, 4), dtype=np.float32)
    pixels[:, 0] = m.reshape((-1, 4))[:, 0]
    pixels[:, 3] = 1 - r.reshape((-1, 4))[:, 0]
    packed = bpy.data.images.new(key + 'MetalSmooth', width=metal.size[0], height=metal.size[1], alpha=True)
    packed.colorspace_settings.name = 'Non-Color'
    packed.pixels.foreach_set(pixels.ravel())
    packed.filepath_raw = os.path.join(folder, key + 'MetalSmooth.png')
    packed.file_format = 'PNG'
    packed.save()
    for image in images:
        if not image.packed_file:
            image.pack()

def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    scene.collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    obj.empty_display_type = 'PLAIN_AXES'
    obj.empty_display_size = .015
    return obj

def mesh(key, name, parent, transform):
    source = sources[key]
    obj = source.copy()
    obj.data = source.data.copy()
    obj.name = name
    scene.collection.objects.link(obj)
    for vertex in obj.data.vertices:
        vertex.co = transform(source.matrix_world @ vertex.co)
    obj.matrix_world = Matrix.Identity(4)
    obj.parent = parent
    obj.data.update()
    return obj

settings = {
    'Pistol': {'length': .45, 'muzzle': (.11, .41), 'barrel_z': .34, 'lock': (.062, .34, .075), 'trigger': (.14, .274), 'trigger_size': .030, 'hammer_size': .073},
    'Musket': {'length': 1.18, 'muzzle': (.075, 1.15), 'barrel_z': .156, 'lock': (.031, .159, .097), 'trigger': (.173, .113), 'trigger_size': .041, 'hammer_size': .084},
    'Shotgun': {'length': .72, 'muzzle': (.079, .74), 'barrel_z': .265, 'lock': (.080, .241, .034), 'trigger': (.126, .162), 'trigger_size': .042, 'hammer_size': .080}
}
roots = []
report = {}
for key, data in settings.items():
    source = sources[key]
    points = [source.matrix_world @ v.co for v in source.data.vertices]
    low_y = min(v.y for v in points)
    high_y = max(v.y for v in points)
    scale = data['length'] / (high_y - low_y)
    origin_y = low_y + data['muzzle'][1] / scale
    origin_z = data['barrel_z'] - data['muzzle'][0] / scale
    def body_point(p):
        return Vector((p.x * scale, (p.y - origin_y) * scale, (p.z - origin_z) * scale))
    root = empty(key + 'Assembly')
    roots.append(root)
    mesh(key, key + 'Body', root, body_point)
    trigger_origin = body_point(Vector((0, data['trigger'][0], data['trigger'][1])))
    trigger = empty('TriggerPivot_' + key, root, trigger_origin)
    trigger_scale = data['trigger_size'] / .9802856
    trigger_center = Vector((-.18, 0, .86))
    def trigger_point(p):
        d = (p - trigger_center) * trigger_scale
        return Vector((d.y, -d.x, d.z))
    mesh('Trigger', 'TriggerMesh_' + key, trigger, trigger_point)
    sides = [1, -1] if key == 'Shotgun' else [1]
    for side in sides:
        suffix = ('Right' if side == 1 else 'Left') + '_' + key
        lock = data['lock']
        hinge = body_point(Vector((lock[0] * side, lock[2], lock[1])))
        hammer = empty('FlintHammerPivot' + suffix, root, hinge)
        hammer_scale = data['hammer_size'] / .983429
        hammer_center = Vector((-.14, 0, .15))
        cock = Matrix.Rotation(math.radians(-25), 4, 'X')
        def hammer_point(p):
            d = (p - hammer_center) * hammer_scale
            return cock @ Vector((d.y * side, -d.x, d.z))
        h = mesh('Hammer', 'FlintHammerMesh' + suffix, hammer, hammer_point)
        frizzen_scale = hammer_scale * .96
        forward = .064 * hammer_scale / (.073 / .983429)
        frizzen = empty('FrizzenPivot' + suffix, root, hinge + Vector((0, -forward, .004)))
        frizzen_center = Vector((.415, 0, .08))
        def frizzen_point(p):
            d = (p - frizzen_center) * frizzen_scale
            return Vector((d.y * side, -d.x, d.z))
        f = mesh('Frizzen', 'FrizzenMesh' + suffix, frizzen, frizzen_point)
        if side == -1:
            for obj in [h, f]:
                for polygon in obj.data.polygons:
                    polygon.flip()
                obj.data.update()
        empty('FlintStrike' + suffix, root, hinge + Vector((side * .001, -forward + .009, .024)))
    empty('Muzzle_' + key, root, (0, -data['muzzle'][1], data['muzzle'][0]))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [root] + list(root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.context.view_layer.update()
    path = os.path.join(exports, key + 'Assembly.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_anim=False, add_leaf_bones=False, path_mode='STRIP')
    report[key] = {'fbx': path, 'scale': scale, 'trigger': list(trigger_origin), 'locks': len(sides)}
bpy.data.libraries.write(os.path.join(art, 'FirearmAssemblies.blend'), {scene}, path_remap='RELATIVE', fake_user=True, compress=True)
bpy.context.window.scene = previous
result = report
