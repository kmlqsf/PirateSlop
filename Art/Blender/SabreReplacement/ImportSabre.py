import bpy
import os
import shutil
import numpy as np
from mathutils import Vector, Matrix

project = r'D:\projects\Pirate_BR\PirateGame'
art = os.path.join(project, 'Art', 'Blender', 'SabreReplacement')
exports = os.path.join(project, 'Assets', 'Models', 'Sabre')
previous = bpy.context.window.scene
scene = bpy.data.scenes.new('SabreAssembly')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
try:
    source = bpy.data.objects.get('Sabre_Source')
    if source is None:
        folder = os.path.join(art, 'Sources')
        path = next(os.path.join(folder, f) for f in os.listdir(folder) if f.lower().endswith('.fbx'))
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=path)
        source = next(o for o in bpy.data.objects if o not in before and o.type == 'MESH')
        source.name = 'Sabre_Source'
    folder = os.path.join(exports, 'Textures')
    os.makedirs(folder, exist_ok=True)
    material = source.data.materials[0]
    material.name = 'Sabre_SourceMaterial'
    images = [n.image for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
    base = next(i for i in images if 'basecolor' in i.name)
    normal = next(i for i in images if 'normal' in i.name)
    metal = next(i for i in images if 'metallic' in i.name)
    rough = next(i for i in images if 'roughness' in i.name)
    shutil.copyfile(bpy.path.abspath(base.filepath), os.path.join(folder, 'SabreBaseColor.jpg'))
    shutil.copyfile(bpy.path.abspath(normal.filepath), os.path.join(folder, 'SabreNormal.png'))
    count = metal.size[0] * metal.size[1]
    m = np.empty(count * 4, dtype=np.float32)
    r = np.empty(count * 4, dtype=np.float32)
    metal.pixels.foreach_get(m)
    rough.pixels.foreach_get(r)
    pixels = np.zeros((count, 4), dtype=np.float32)
    pixels[:, 0] = m.reshape((-1, 4))[:, 0]
    pixels[:, 3] = 1 - r.reshape((-1, 4))[:, 0]
    packed = bpy.data.images.new('SabreMetalSmooth', width=metal.size[0], height=metal.size[1], alpha=True)
    packed.colorspace_settings.name = 'Non-Color'
    packed.pixels.foreach_set(pixels.ravel())
    packed.filepath_raw = os.path.join(folder, 'SabreMetalSmooth.png')
    packed.file_format = 'PNG'
    packed.save()
    for image in images:
        if not image.packed_file:
            image.pack()
    root = bpy.data.objects.new('SabreAssembly', None)
    scene.collection.objects.link(root)
    model = source.copy()
    model.data = source.data.copy()
    model.name = 'SabreBody'
    scene.collection.objects.link(model)
    for vertex in model.data.vertices:
        p = source.matrix_world @ vertex.co
        vertex.co = Vector((p.x + .0345, -(p.z - .144), .335 - p.y)) * .75
    model.matrix_world = Matrix.Identity(4)
    model.parent = root
    model.data.update()
    for name, location in [('GripSocket_Cutlass', (0, 0, 0)), ('BladeCollar', (0, 0, .14)), ('BladeTipSocket', (0, -.057, .618))]:
        socket = bpy.data.objects.new(name, None)
        scene.collection.objects.link(socket)
        socket.parent = root
        socket.location = location
        socket.empty_display_size = .015
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [root] + list(root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=os.path.join(exports, 'SabreAssembly.fbx'), use_selection=True, object_types={'EMPTY', 'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_anim=False, add_leaf_bones=False, path_mode='STRIP')
    bpy.data.libraries.write(os.path.join(art, 'SabreAssembly.blend'), {scene}, path_remap='RELATIVE', fake_user=True, compress=True)
    result = {'vertices': len(model.data.vertices), 'length': .735, 'grip': 'GripSocket_Cutlass', 'export': os.path.join(exports, 'SabreAssembly.fbx')}
finally:
    bpy.context.window.scene = previous
