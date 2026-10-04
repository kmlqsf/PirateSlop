import bpy
import math
from pathlib import Path
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[4]
SOURCE = Path(__file__).resolve().parent
EXPORT = ROOT / "Assets/Models/Ships/ShipNameplate"
OWNER = "ShipNameplateGeneratorV1"
previous_scene = bpy.context.window.scene
previous_filepath = bpy.data.filepath
previous_selection = list(bpy.context.selected_objects)
previous_active = bpy.context.view_layer.objects.active
scene = bpy.data.scenes.get("ShipNameplateAuthoring")
if scene and scene.get("generator") != OWNER:
    raise RuntimeError("ShipNameplateAuthoring is not owned by this generator")
if scene is None:
    scene = bpy.data.scenes.new("ShipNameplateAuthoring")
scene["generator"] = OWNER
for obj in list(scene.objects):
    if obj.get("generator") == OWNER:
        bpy.data.objects.remove(obj, do_unlink=True)
model_collection = bpy.data.collections.get("ShipNameplateModel")
if model_collection is None:
    model_collection = bpy.data.collections.new("ShipNameplateModel")
if model_collection.name not in scene.collection.children:
    scene.collection.children.link(model_collection)
preview_collection = bpy.data.collections.get("ShipNameplatePreview")
if preview_collection is None:
    preview_collection = bpy.data.collections.new("ShipNameplatePreview")
if preview_collection.name not in scene.collection.children:
    scene.collection.children.link(preview_collection)
bpy.context.window.scene = scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.get(name)
    if mat and mat.get("generator") != OWNER:
        raise RuntimeError(name + " is not owned by this generator")
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat["generator"] = OWNER
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    output = mat.node_tree.nodes.new("ShaderNodeOutputMaterial")
    shader = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    mat.node_tree.links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return mat, shader


wood, wood_shader = material("ShipNameplateWood", (0.070, 0.035, 0.014), 0.0, 0.70)
wood_edge, _ = material("ShipNameplateWoodEdge", (0.065, 0.022, 0.009), 0.0, 0.72)
brass, brass_shader = material("ShipNameplateBrass", (0.53, 0.31, 0.10), 0.82, 0.33)
highlight, _ = material("ShipNameplateBrassHighlight", (0.72, 0.49, 0.18), 0.88, 0.26)
atlas_path = ROOT / "Assets/Models/Ships/MainShip/Textures/StylShip_Masts_BaseColor.png"
if atlas_path.exists():
    atlas = bpy.data.images.load(str(atlas_path), check_existing=True)
    tex = wood.node_tree.nodes.new("ShaderNodeTexImage")
    tex.image = atlas
    luminance = wood.node_tree.nodes.new("ShaderNodeRGBToBW")
    walnut = wood.node_tree.nodes.new("ShaderNodeValToRGB")
    walnut.color_ramp.elements[0].position = 0.035
    walnut.color_ramp.elements[0].color = (0.022, 0.011, 0.004, 1.0)
    walnut.color_ramp.elements[1].position = 0.16
    walnut.color_ramp.elements[1].color = (0.078, 0.038, 0.014, 1.0)
    wood.node_tree.links.new(tex.outputs["Color"], luminance.inputs[0])
    wood.node_tree.links.new(luminance.outputs[0], walnut.inputs["Fac"])
    wood.node_tree.links.new(walnut.outputs["Color"], wood_shader.inputs["Base Color"])
    bump = wood.node_tree.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 0.10
    bump.inputs["Distance"].default_value = 0.003
    wood.node_tree.links.new(tex.outputs["Color"], bump.inputs["Height"])
    wood.node_tree.links.new(bump.outputs["Normal"], wood_shader.inputs["Normal"])
noise = brass.node_tree.nodes.new("ShaderNodeTexNoise")
noise.inputs["Scale"].default_value = 52.0
noise.inputs["Detail"].default_value = 1.5
ramp = brass.node_tree.nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].position = 0.18
ramp.color_ramp.elements[0].color = (0.34, 0.17, 0.044, 1.0)
ramp.color_ramp.elements[1].position = 0.78
ramp.color_ramp.elements[1].color = (0.64, 0.39, 0.13, 1.0)
brass.node_tree.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
brass.node_tree.links.new(ramp.outputs["Color"], brass_shader.inputs["Base Color"])


def signed_area(points):
    return sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:] + points[:1]))


def mesh_object(name, vertices, faces, mat, bevel=0.0):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    obj["generator"] = OWNER
    model_collection.objects.link(obj)
    obj.data.materials.append(mat)
    uv = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            v = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (0.382 + ((v.z + 0.45) / 0.90) * 0.033, 0.215 + ((v.x + 2.8) / 5.6) * 0.30)
    if bevel:
        mod = obj.modifiers.new("EdgeBevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.affect = "EDGES"
        mod.limit_method = "ANGLE"
        weighted = obj.modifiers.new("WeightedNormals", "WEIGHTED_NORMAL")
        weighted.keep_sharp = True
        weighted.weight = 40
    return obj


def solid_polygon(name, points, front, back, mat, bevel=0.0):
    if signed_area(points) < 0:
        points = list(reversed(points))
    n = len(points)
    vertices = [(x, front, z) for x, z in points] + [(x, back, z) for x, z in points]
    faces = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))]
    faces += [(i, n + i, n + (i + 1) % n, (i + 1) % n) for i in range(n)]
    return mesh_object(name, vertices, faces, mat, bevel)


def band(name, outer, inner, front, back, mat, bevel=0.0):
    if signed_area(outer) < 0:
        outer = list(reversed(outer))
        inner = list(reversed(inner))
    n = len(outer)
    vertices = [(x, front, z) for x, z in outer] + [(x, front, z) for x, z in inner]
    vertices += [(x, back, z) for x, z in outer] + [(x, back, z) for x, z in inner]
    faces = []
    for i in range(n):
        j = (i + 1) % n
        faces += [(i, j, n + j, n + i), (2 * n + i, 3 * n + i, 3 * n + j, 2 * n + j)]
        faces += [(i, 2 * n + i, 2 * n + j, j), (n + j, 3 * n + j, 3 * n + i, n + i)]
    return mesh_object(name, vertices, faces, mat, bevel)


half_top = [(0.0, 0.358), (0.8, 0.365), (1.55, 0.377), (2.15, 0.405), (2.35, 0.45), (2.42, 0.354), (2.58, 0.272), (2.73, 0.205), (2.8, 0.082), (2.8, 0.0)]
right = half_top + [(x, -z) for x, z in reversed(half_top[:-1])]
outer = right + [(-x, z) for x, z in reversed(right[1:-1])]
backing = solid_polygon("ShipNameplateBacking", outer, 0.018, 0.13, wood_edge, 0.007)
frame_outer = [(x * 0.986, z * 0.965) for x, z in outer]
frame_inner = [(x * 0.959, z * 0.795) for x, z in frame_outer]
frame = band("ShipNameplateBrassFrame", frame_outer, frame_inner, -0.021, 0.014, brass, 0.009)
ridge_outer = [(x * 0.989, z * 0.983) for x, z in frame_outer]
ridge_inner = [(x * 0.992, z * 0.954) for x, z in ridge_outer]
band("ShipNameplateOuterHighlight", ridge_outer, ridge_inner, -0.026, -0.018, highlight, 0.003)
inner_ridge_outer = [(x * 1.001, z * 1.019) for x, z in frame_inner]
inner_ridge_inner = [(x * 0.996, z * 0.969) for x, z in frame_inner]
band("ShipNameplateInnerHighlight", inner_ridge_outer, inner_ridge_inner, -0.026, -0.015, highlight, 0.003)
panel = [(-2.10, 0.310), (-1.55, 0.308), (-0.8, 0.298), (0.0, 0.291), (0.8, 0.298), (1.55, 0.308), (2.10, 0.310), (2.18, 0.239), (2.21, 0.083), (2.21, -0.083), (2.18, -0.239), (2.10, -0.310), (1.55, -0.308), (0.8, -0.298), (0.0, -0.291), (-0.8, -0.298), (-1.55, -0.308), (-2.10, -0.310), (-2.18, -0.239), (-2.21, -0.083), (-2.21, 0.083), (-2.18, 0.239)]
panel_object = solid_polygon("ShipNameplateNameField", panel, 0.0, 0.02, wood, 0.005)
panel_object["clear_text_width_m"] = 4.12
panel_object["clear_text_height_m"] = 0.52
end_base = [(2.12, 0.316), (2.35, 0.413), (2.39, 0.321), (2.54, 0.253), (2.69, 0.168), (2.74, 0.05), (2.70, -0.141), (2.54, -0.253), (2.39, -0.321), (2.35, -0.413), (2.12, -0.316), (2.20, -0.190), (2.21, 0.19)]
for sign, label in [(-1, "Port"), (1, "Starboard")]:
    solid_polygon("ShipNameplate" + label + "CarvedEnd", [(sign * x, z) for x, z in end_base], -0.008, 0.02, wood, 0.007)


def ribbon(name, points, widths, front=-0.043, back=-0.012):
    left = []
    right = []
    for i, p in enumerate(points):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, len(points) - 1)]
        dx, dz = b[0] - a[0], b[1] - a[1]
        length = max(math.hypot(dx, dz), 0.00001)
        nx, nz = -dz / length, dx / length
        left.append((p[0] + nx * widths[i] / 2, p[1] + nz * widths[i] / 2))
        right.append((p[0] - nx * widths[i] / 2, p[1] - nz * widths[i] / 2))
    n = len(points)
    vertices = [(x, front, z) for x, z in left + right] + [(x, back, z) for x, z in left + right]
    faces = []
    for i in range(n - 1):
        j = i + 1
        faces += [(i, n + i, n + j, j), (2 * n + i, 2 * n + j, 3 * n + j, 3 * n + i)]
        faces += [(i, j, 2 * n + j, 2 * n + i), (n + j, n + i, 3 * n + i, 3 * n + j)]
    faces += [(0, 2 * n, 3 * n, n), (n - 1, 2 * n - 1, 4 * n - 1, 3 * n - 1)]
    return mesh_object(name, vertices, faces, brass, 0.008)


def bezier(a, b, c, d, count):
    values = []
    for i in range(count):
        t = i / (count - 1)
        values.append(tuple((1 - t) ** 3 * a[k] + 3 * (1 - t) ** 2 * t * b[k] + 3 * (1 - t) * t ** 2 * c[k] + t ** 3 * d[k] for k in range(2)))
    return values


for sign, side in [(-1, "Port"), (1, "Starboard")]:
    for lower in [False, True]:
        cx = 2.45 if not lower else 2.36
        cz = 0.097 if not lower else -0.165
        r0 = 0.157 if not lower else 0.148
        start = 1.1 if not lower else 3.32
        sense = -1.0 if not lower else 1.0
        spiral = []
        for i in range(33):
            t = i / 32
            angle = start + sense * t * math.pi * 1.88
            radius = r0 * (1.0 - 0.80 * t)
            spiral.append((cx + radius * math.cos(angle), cz + radius * math.sin(angle)))
        first = spiral[0]
        stem_start = (2.34, 0.385) if not lower else (2.34, -0.383)
        control_a = (2.33, 0.286) if not lower else (2.27, -0.305)
        control_b = (2.48, 0.239) if not lower else (2.22, -0.225)
        stem = bezier(stem_start, control_a, control_b, first, 8)[:-1]
        points = [(sign * x, z) for x, z in stem + spiral]
        widths = [0.062 + 0.033 * i / 7 for i in range(len(stem))]
        widths += [0.095 * (1.0 - 0.72 * i / 32) for i in range(33)]
        ribbon("ShipNameplate" + side + ("LowerScroll" if lower else "UpperScroll"), points, widths)
    leaf = [(2.165, 0.357), (2.34, 0.410), (2.405, 0.307), (2.535, 0.232), (2.37, 0.254), (2.27, 0.312)]
    solid_polygon("ShipNameplate" + side + "UpperLeaf", [(sign * x, z) for x, z in leaf], -0.037, -0.018, highlight, 0.005)
    solid_polygon("ShipNameplate" + side + "LowerLeaf", [(sign * x, -z) for x, z in leaf], -0.037, -0.018, highlight, 0.005)


def dome(name, x, z, radius, lowest_y):
    levels = [(0.0, lowest_y), (0.36 * radius, lowest_y + 0.004), (0.72 * radius, lowest_y + 0.012), (radius, lowest_y + 0.028), (radius, lowest_y + 0.045)]
    vertices = []
    count = 12
    for r, y in levels:
        for i in range(count):
            a = 2 * math.pi * i / count
            vertices.append((x + r * math.cos(a), y, z + r * math.sin(a)))
    faces = []
    for level in range(len(levels) - 1):
        for i in range(count):
            j = (i + 1) % count
            faces.append((level * count + i, (level + 1) * count + i, (level + 1) * count + j, level * count + j))
    faces.append(tuple(reversed(range((len(levels) - 1) * count, len(levels) * count))))
    return mesh_object(name, vertices, faces, highlight)


for sign, side in [(-1, "Port"), (1, "Starboard")]:
    dome("ShipNameplate" + side + "MainRivet", sign * 2.205, 0.211, 0.059, -0.05)
    for lower in [False, True]:
        dome("ShipNameplate" + side + ("LowerPin" if lower else "UpperPin"), sign * 2.345, -0.391 if lower else 0.391, 0.012, -0.029)


for obj in model_collection.objects:
    obj["visual_only"] = True
    obj["front_axis_blender"] = "-Y"
    obj["front_axis_unity"] = "-Z"
scene["unity_dimensions_m"] = [5.6, 0.9, 0.18]
scene["unity_front_axis"] = "-Z"
scene["unity_text_origin"] = [0.0, 0.0, 0.0]
scene["wood_atlas_uv"] = [0.382, 0.215, 0.033, 0.30]
scene["wood_tint"] = [0.25, 0.34, 0.30, 1.0]


def preview_object(name, data):
    obj = bpy.data.objects.new(name, data)
    obj["generator"] = OWNER
    preview_collection.objects.link(obj)
    return obj


camera_data = bpy.data.cameras.new("ShipNameplatePreviewCamera")
camera = preview_object("ShipNameplatePreviewCamera", camera_data)
camera.location = (0.22, -8.0, 1.25)
direction = Vector((0.0, 0.0, 0.0)) - camera.location
camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
camera_data.type = "ORTHO"
camera_data.ortho_scale = 6.35
scene.camera = camera
for name, location, energy, size in [
    ("ShipNameplateKey", (-3.0, -4.0, 5.5), 950, 5.0),
    ("ShipNameplateFill", (3.5, -3.0, 1.8), 500, 4.0),
    ("ShipNameplateRim", (1.2, 1.0, 4.5), 600, 3.0),
]:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.shape = "DISK"
    data.size = size
    lamp = preview_object(name, data)
    lamp.location = location
    lamp.rotation_euler = (Vector((0.0, 0.0, 0.0)) - lamp.location).to_track_quat("-Z", "Y").to_euler()
world = bpy.data.worlds.new("ShipNameplatePreviewWorld")
world.use_nodes = True
world.node_tree.nodes.clear()
world_background = world.node_tree.nodes.new("ShaderNodeBackground")
world_output = world.node_tree.nodes.new("ShaderNodeOutputWorld")
world_background.inputs[0].default_value = (0.12, 0.12, 0.12, 1.0)
world_background.inputs[1].default_value = 0.65
world.node_tree.links.new(world_background.outputs[0], world_output.inputs[0])
scene.world = world
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 1600
scene.render.resolution_y = 540
scene.render.resolution_percentage = 100
scene.render.film_transparent = False
scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
SOURCE.mkdir(parents=True, exist_ok=True)
EXPORT.mkdir(parents=True, exist_ok=True)
bpy.context.view_layer.update()
for obj in bpy.context.selected_objects:
    obj.select_set(False)
for obj in model_collection.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = backing
export_path = EXPORT / "ShipNameplate.fbx"
bpy.ops.export_scene.fbx(filepath=str(export_path), use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", bake_space_transform=True, axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False, path_mode="AUTO", embed_textures=False)
source_path = SOURCE / "ShipNameplate.blend"
bpy.data.libraries.write(str(source_path), {scene}, path_remap="RELATIVE", fake_user=True, compress=True)
preview_path = SOURCE / "ShipNameplatePreview.png"
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = str(preview_path)
bpy.ops.render.render(write_still=True, scene=scene.name)
depsgraph = bpy.context.evaluated_depsgraph_get()
points = []
triangles = 0
for obj in model_collection.objects:
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    triangles += len(mesh.loop_triangles)
    points += [obj.matrix_world @ v.co for v in mesh.vertices]
    evaluated.to_mesh_clear()
minimum = [min(p[k] for p in points) for k in range(3)]
maximum = [max(p[k] for p in points) for k in range(3)]
dimensions_blender = [maximum[k] - minimum[k] for k in range(3)]
bpy.context.window.scene = previous_scene
for obj in previous_selection:
    if obj.name in previous_scene.objects:
        obj.select_set(True)
if previous_active and previous_active.name in previous_scene.objects:
    bpy.context.view_layer.objects.active = previous_active
if bpy.data.filepath != previous_filepath:
    raise RuntimeError("The current Blender filepath unexpectedly changed")
result = {"source": str(source_path), "export": str(export_path), "preview": str(preview_path), "objects": len(model_collection.objects), "triangles": triangles, "dimensions_blender": dimensions_blender, "bounds_blender": [minimum, maximum], "materials": [wood.name, wood_edge.name, brass.name, highlight.name], "restored_scene": bpy.context.scene.name, "current_filepath": bpy.data.filepath}
