import bpy
from pathlib import Path
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parents[3]
scene = bpy.data.scenes['BarricadeAuthoring']
previous_scene = bpy.context.window.scene
previous_active = bpy.context.view_layer.objects.active
previous_selected = list(bpy.context.selected_objects)
bpy.context.window.scene = scene
for existing in list(scene.objects):
    if existing.name == 'BarricadeWood' or existing.name.startswith('BarricadeFragment_'):
        bpy.data.objects.remove(existing, do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(root / 'Art/Blender/Barricade/Source/tripo_convert_b15e68d3-b748-4ef3-8a7f-3a6d42427a7f.fbx'))
obj = next(o for o in scene.objects if o.type == 'MESH')
obj.name = 'BarricadeWood'
points = [obj.matrix_world @ v.co for v in obj.data.vertices]
low = Vector(tuple(min(v[i] for v in points) for i in range(3)))
high = Vector(tuple(max(v[i] for v in points) for i in range(3)))

def remap(value, inputs, outputs):
    for i in range(len(inputs) - 1):
        if value <= inputs[i + 1]:
            t = (value - inputs[i]) / (inputs[i + 1] - inputs[i])
            return outputs[i] + t * (outputs[i + 1] - outputs[i])
    return outputs[-1]

for vertex, point in zip(obj.data.vertices, points):
    vertex.co = Vector((
        remap(point.x, [low.x, -.060, .050, high.x], [-.70, -.34, .34, .70]),
        (point.y - (low.y + high.y) * .5) * (.68 / (high.y - low.y)),
        remap(point.z, [low.z, .495, .590, high.z], [0, 1.28, 1.82, 2.10])))
obj.matrix_world = Matrix.Identity(4)
obj.data.update()
scene['opening_height'] = '1.42-1.69 m clear region; eye 1.65 m'
scene['source'] = 'Blender/Лутабельные/Баррикада.zip'
folder = root / 'Assets/Models/Barricade'
folder.mkdir(parents=True, exist_ok=True)
import shutil
import numpy as np
textures = folder / 'Textures'
textures.mkdir(exist_ok=True)
source_textures = root / 'Art/Blender/Barricade/Source/tripo_convert_b15e68d3-b748-4ef3-8a7f-3a6d42427a7f.fbm'
shutil.copyfile(source_textures / 'Баррикада_basecolor.JPEG', textures / 'BarricadeBaseColor.jpg')
shutil.copyfile(source_textures / 'Баррикада_normal.PNG', textures / 'BarricadeNormal.png')
metal = bpy.data.images.load(str(source_textures / 'Баррикада_metallic.JPEG'), check_existing=True)
rough = bpy.data.images.load(str(source_textures / 'Баррикада_roughness.JPEG'), check_existing=True)
metal.colorspace_settings.name = 'Non-Color'
rough.colorspace_settings.name = 'Non-Color'
pixels = np.empty(len(metal.pixels), dtype=np.float32)
metal.pixels.foreach_get(pixels)
rough_pixels = np.empty(len(rough.pixels), dtype=np.float32)
rough.pixels.foreach_get(rough_pixels)
pixels[1::4] = 0
pixels[2::4] = 0
pixels[3::4] = 1 - rough_pixels[0::4]
packed = bpy.data.images.new('BarricadeMetalSmooth', width=metal.size[0], height=metal.size[1], alpha=True)
packed.colorspace_settings.name = 'Non-Color'
packed.pixels.foreach_set(pixels)
packed.filepath_raw = str(textures / 'BarricadeMetalSmooth.png')
packed.file_format = 'PNG'
packed.save()
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.export_scene.fbx(filepath=str(folder / 'Barricade.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
    bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type='FACE')

fragments = []
for row in range(4):
    for column in range(4):
        part = obj.copy()
        part.data = obj.data.copy()
        part.name = f'BarricadeFragment_{row}_{column}'
        scene.collection.objects.link(part)
        import bmesh
        bm = bmesh.new()
        bm.from_mesh(part.data)
        for face in list(bm.faces):
            center = face.calc_center_median()
            r = min(3, int(max(0, center.z) / 2.1 * 4))
            c = min(3, int(max(0, center.x + .7) / 1.4 * 4))
            if r != row or c != column:
                bm.faces.remove(face)
        isolated = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=isolated, context='VERTS')
        if not bm.faces:
            bm.free()
            bpy.data.objects.remove(part, do_unlink=True)
            continue
        bm.to_mesh(part.data)
        bm.free()
        center = sum((v.co for v in part.data.vertices), Vector()) / len(part.data.vertices)
        for v in part.data.vertices:
            v.co -= center
        part.location = center
        part.data.update()
        fragments.append(part)

bpy.ops.object.select_all(action='DESELECT')
for part in fragments:
    part.select_set(True)
bpy.context.view_layer.objects.active = fragments[0]
bpy.ops.export_scene.fbx(filepath=str(folder / 'BarricadeFragments.fbx'), use_selection=True,
    object_types={'MESH'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
    bake_anim=False, use_mesh_modifiers=True, mesh_smooth_type='FACE')
for part in fragments:
    part.hide_set(True)
    part.hide_render = True
bpy.data.libraries.write(str(root / 'Art/Blender/Barricade/Barricade.blend'), {scene},
    path_remap='ABSOLUTE', fake_user=True, compress=True)
tree = BVHTree.FromPolygons([v.co for v in obj.data.vertices], [tuple(p.vertices) for p in obj.data.polygons])
clear = all(tree.ray_cast(Vector((x, -2, z)), Vector((0, 1, 0)), 4)[0] is None
    for x in [-.28, -.14, 0, .14, .28] for z in [1.42, 1.5, 1.6, 1.65, 1.69])
bpy.context.window.scene = previous_scene
bpy.context.view_layer.objects.active = previous_active
for selected in previous_selected:
    selected.select_set(True)
result = {'size': [1.4, .68, 2.1], 'clear_firing_region': clear,
    'fragments': len(fragments), 'file': str(root / 'Art/Blender/Barricade/Barricade.blend')}
