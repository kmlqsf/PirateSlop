import bpy
import math
import os
from mathutils import Vector

root = r'D:\projects\Pirate_BR\PirateGame'
check = bpy.data.objects.new('HolyGrenadeConnectionCheck', None)
bpy.context.scene.collection.objects.link(check)
check.location = (1, 2, 3)
assert tuple(check.location) == (1, 2, 3)
bpy.data.objects.remove(check, do_unlink=True)
original_scene = bpy.data.scenes.get('Scene') or bpy.context.window.scene
previous = bpy.data.scenes.get('HolyGrenadeAsset')
if previous is not None:
    bpy.context.window.scene = original_scene
    bpy.data.scenes.remove(previous)
scene = bpy.data.scenes.new('HolyGrenadeAsset')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1

def material(name, color, metal=0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = next((node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
    if shader is None:
        shader = mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metal
    shader.inputs['Roughness'].default_value = .34
    return mat

white = material('HolyIvory', (.92, .93, .89))
gold = material('HolyGold', (.72, .43, .085), .65)
rope = material('HolyWick', (.18, .105, .04))
red = material('HolyRuby', (.44, .015, .025), .25)
objects = []

def finish(obj, name, mat):
    obj.name = name
    obj.data.materials.append(mat)
    objects.append(obj)
    return obj

bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=.13)
finish(bpy.context.object, 'IvoryOrb', white)
for p in bpy.context.object.data.polygons:
    p.use_smooth = True

for rotation in [(0, 0, 0), (math.pi/2, 0, 0)]:
    bpy.ops.mesh.primitive_torus_add(major_segments=32, minor_segments=6, major_radius=.129, minor_radius=.007, rotation=rotation)
    finish(bpy.context.object, 'GoldBand', gold)

bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=1, location=(0, 0, .122))
cap = finish(bpy.context.object, 'CrownSocket', gold)
cap.scale = (.046, .046, .022)

def box(name, location, dimensions):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = finish(bpy.context.object, name, gold)
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel = obj.modifiers.new('SoftEdges', 'BEVEL')
    bevel.width = .003
    bevel.segments = 2
    obj.modifiers.new('WeightedNormals', 'WEIGHTED_NORMAL')
    return obj

box('CrossStem', (0, 0, .191), (.024, .022, .116))
box('CrossArms', (0, 0, .214), (.083, .022, .024))
for y in [-.014, .014]:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=.014, location=(0, y, .214))
    gem = finish(bpy.context.object, 'CrossRuby', red)
    gem.scale.y = .5

curve = bpy.data.curves.new('WickCurve', 'CURVE')
curve.dimensions = '3D'
curve.bevel_depth = .0055
curve.bevel_resolution = 1
curve.resolution_u = 8
spline = curve.splines.new('BEZIER')
spline.bezier_points.add(2)
for point, co in zip(spline.bezier_points, [(0,0,0),(.015,0,.045),(.012,0,.09)]):
    point.co = co
    point.handle_left_type = point.handle_right_type = 'AUTO'
wick = bpy.data.objects.new('FuseWick', curve)
scene.collection.objects.link(wick)
wick.location = (.061, 0, .112)
wick.data.materials.append(rope)
objects.append(wick)
bpy.context.view_layer.objects.active = wick
wick.select_set(True)
bpy.ops.object.convert(target='MESH')
objects[-1] = bpy.context.object
for obj in scene.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = objects[0]
os.makedirs(root + '/Assets/Models/HolyGrenade', exist_ok=True)
os.makedirs(root + '/Art/Blender/HolyGrenade', exist_ok=True)
bpy.ops.export_scene.fbx(filepath=root + '/Assets/Models/HolyGrenade/HolyGrenade.fbx', use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_space_transform=True, add_leaf_bones=False)
bpy.data.libraries.write(root + '/Art/Blender/HolyGrenade/HolyGrenade.blend', {scene}, fake_user=True)
bpy.context.window.scene = original_scene
result = {'objects': len(objects), 'fbx': root + '/Assets/Models/HolyGrenade/HolyGrenade.fbx', 'blend': root + '/Art/Blender/HolyGrenade/HolyGrenade.blend'}
