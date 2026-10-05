import bpy
import bmesh
import json
import math
import struct
import time
from pathlib import Path
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[4]
SOURCE = Path(__file__).resolve().parent
EXPORT = ROOT / "Assets/Models/Ships/ShipNameplate"
FONT_PATH = Path("C:/Windows/Fonts/georgiab.ttf")
OWNER = "ShipNameGlyphGeneratorV1"
STATE = SOURCE / "glyph_generation_state.json"
METRICS = EXPORT / "ShipNameGlyphs.json"
CHARACTERS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" + "".join(chr(i) for i in range(0x0410, 0x0450)) + "Ёё-'.()!?_:,\"&+"
CHARS = list(dict.fromkeys(CHARACTERS))
STROKE_EXPANSION = 0.004
RIM_WIDTH = 0.032
MITER_LIMIT = 1.5


def read_font_metrics(path):
    data = path.read_bytes()
    count = struct.unpack_from(">H", data, 4)[0]
    tables = {}
    for i in range(count):
        tag, checksum, offset, size = struct.unpack_from(">4sIII", data, 12 + i * 16)
        tables[tag.decode("ascii")] = (offset, size)
    cmap_start = tables["cmap"][0]
    cmap_count = struct.unpack_from(">H", data, cmap_start + 2)[0]
    candidates = []
    for i in range(cmap_count):
        platform, encoding, offset = struct.unpack_from(">HHI", data, cmap_start + 4 + i * 8)
        absolute = cmap_start + offset
        format_id = struct.unpack_from(">H", data, absolute)[0]
        if format_id in (4, 12):
            candidates.append((format_id == 12, platform == 3, absolute, format_id))
    _, _, cmap, format_id = sorted(candidates)[-1]
    character_map = {}
    if format_id == 12:
        groups = struct.unpack_from(">I", data, cmap + 12)[0]
        for i in range(groups):
            first, last, glyph = struct.unpack_from(">III", data, cmap + 16 + i * 12)
            for codepoint in range(first, last + 1):
                character_map[codepoint] = glyph + codepoint - first
    else:
        segments = struct.unpack_from(">H", data, cmap + 6)[0] // 2
        ends = cmap + 14
        starts = ends + 2 * segments + 2
        deltas = starts + 2 * segments
        ranges = deltas + 2 * segments
        for i in range(segments):
            first = struct.unpack_from(">H", data, starts + 2 * i)[0]
            last = struct.unpack_from(">H", data, ends + 2 * i)[0]
            delta = struct.unpack_from(">h", data, deltas + 2 * i)[0]
            range_offset = struct.unpack_from(">H", data, ranges + 2 * i)[0]
            for codepoint in range(first, min(last, 0xFFFE) + 1):
                if range_offset == 0:
                    glyph = (codepoint + delta) & 0xFFFF
                else:
                    glyph_offset = ranges + 2 * i + range_offset + 2 * (codepoint - first)
                    glyph = struct.unpack_from(">H", data, glyph_offset)[0]
                    if glyph:
                        glyph = (glyph + delta) & 0xFFFF
                character_map[codepoint] = glyph
    hhea = tables["hhea"][0]
    ascender, descender = struct.unpack_from(">hh", data, hhea + 4)
    metric_count = struct.unpack_from(">H", data, hhea + 34)[0]
    glyph_count = struct.unpack_from(">H", data, tables["maxp"][0] + 4)[0]
    hmtx = tables["hmtx"][0]
    advances = [struct.unpack_from(">H", data, hmtx + 4 * min(i, metric_count - 1))[0] for i in range(glyph_count)]
    kern = {}
    if "kern" in tables:
        kern_offset, kern_size = tables["kern"]
        version, subtables = struct.unpack_from(">HH", data, kern_offset)
        cursor = kern_offset + 4
        if version == 0:
            for i in range(subtables):
                subversion, size, coverage = struct.unpack_from(">HHH", data, cursor)
                if coverage >> 8 == 0 and coverage & 1:
                    pairs = struct.unpack_from(">H", data, cursor + 6)[0]
                    for pair in range(pairs):
                        left, right, value = struct.unpack_from(">HHh", data, cursor + 14 + pair * 6)
                        if value:
                            kern[(left, right)] = value
                cursor += size
    return character_map, advances, kern, ascender, descender


def owned_collection(name, scene):
    collection = bpy.data.collections.get(name)
    if collection and collection.get("generator") != OWNER:
        raise RuntimeError(name + " is not owned by this generator")
    if collection is None:
        collection = bpy.data.collections.new(name)
    collection["generator"] = OWNER
    if collection.name not in scene.collection.children:
        scene.collection.children.link(collection)
    return collection


def generation_scene():
    scene = bpy.data.scenes.get("ShipNameGlyphAuthoring")
    if scene and scene.get("generator") not in (None, OWNER):
        raise RuntimeError("ShipNameGlyphAuthoring is not owned by this generator")
    if scene is None:
        scene = bpy.data.scenes.new("ShipNameGlyphAuthoring")
    scene["generator"] = OWNER
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def glyph_material():
    name = "ShipNameGlyphFace"
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat["generator"] = OWNER
    mat.diffuse_color = (0.82, 0.64, 0.32, 1.0)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    output = mat.node_tree.nodes.new("ShaderNodeOutputMaterial")
    shader = mat.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    shader.inputs["Base Color"].default_value = mat.diffuse_color
    shader.inputs["Metallic"].default_value = 0.15
    shader.inputs["Roughness"].default_value = 0.70
    mat.node_tree.links.new(shader.outputs["BSDF"], output.inputs[0])
    return mat


def font_mesh(name, char, size, offset, extrude, z, collection, editable=None):
    curve = bpy.data.curves.new(name + "Outline", "FONT")
    curve.body = char
    curve.font = bpy.data.fonts.get("Georgia Bold") or bpy.data.fonts.load(str(FONT_PATH), check_existing=True)
    curve.size = size
    curve.offset = offset
    curve.extrude = extrude
    curve.resolution_u = 4
    curve.fill_mode = "BOTH"
    curve.align_x = "LEFT"
    curve.align_y = "TOP_BASELINE"
    if editable:
        editable_curve = curve.copy()
        editable_curve.offset = 0.0
        editable_curve.extrude = 0.0
        source = bpy.data.objects.new(name + "EditableOutline", editable_curve)
        source["generator"] = OWNER
        source.hide_render = True
        editable.objects.link(source)
        bpy.context.view_layer.update()
        source.hide_set(True)
    obj = bpy.data.objects.new(name, curve)
    obj["generator"] = OWNER
    collection.objects.link(obj)
    obj.location.z = z
    for selected in list(bpy.context.selected_objects):
        selected.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)
    return obj


def bounds(mesh):
    return [[min(v.co[k] for v in mesh.vertices), max(v.co[k] for v in mesh.vertices)] for k in range(3)]


def prepare():
    scene = generation_scene()
    previous = bpy.context.window.scene
    bpy.context.window.scene = scene
    collection = owned_collection("ShipNameGlyphLibrary", scene)
    editable = owned_collection("ShipNameGlyphEditable", scene)
    for obj in list(scene.objects):
        if obj.get("generator") == OWNER:
            bpy.data.objects.remove(obj, do_unlink=True)
    face = glyph_material()
    brass = bpy.data.materials.get("ShipNameplateBrass")
    if brass is None:
        raise RuntimeError("The approved nameplate brass material is unavailable")
    probe = font_mesh("GlyphMetricProbe", "H", 1.0, 0.0, 0.0, 0.0, collection)
    h_bounds = bounds(probe.data)
    cap = h_bounds[1][1] - h_bounds[1][0]
    bpy.data.objects.remove(probe, do_unlink=True)
    probe = font_mesh("GlyphMetricProbe", "HH", 1.0, 0.0, 0.0, 0.0, collection)
    hh_bounds = bounds(probe.data)
    advance_h = hh_bounds[0][1] - h_bounds[0][1]
    bpy.data.objects.remove(probe, do_unlink=True)
    mapping, advances, kern, ascender, descender = read_font_metrics(FONT_PATH)
    unit = advance_h / (advances[mapping[ord("H")]] + kern.get((mapping[ord("H")], mapping[ord("H")]), 0)) / cap
    supported = [ord(c) for c in CHARS] + [32]
    pairs = []
    for left in supported:
        for right in supported:
            value = kern.get((mapping.get(left, 0), mapping.get(right, 0)), 0)
            if value:
                pairs.append({"left": left, "right": right, "offset": value * unit})
    payload = {"font": "Georgia Bold", "capHeight": 1.0, "spaceAdvance": advances[mapping[32]] * unit, "ascender": ascender * unit, "descender": descender * unit, "frontAxis": "-Z", "upAxis": "Y", "materialNames": [face.name, brass.name], "glyphs": [], "kerning": pairs}
    METRICS.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    state = {"size": 1.0 / cap, "unit": unit, "mapping": {str(k): v for k, v in mapping.items() if k in supported}, "advances": advances, "next": 0}
    STATE.write_text(json.dumps(state), encoding="utf-8")
    bpy.context.window.scene = previous
    return {"characters": len(CHARS), "size": state["size"], "spaceAdvance": payload["spaceAdvance"], "kerningPairs": len(pairs), "restored_scene": previous.name}


def contour_area(points):
    return sum(a.x * b.y - b.x * a.y for a, b in zip(points, points[1:] + points[:1])) / 2


def contains(points, point):
    inside = False
    for a, b in zip(points, points[1:] + points[:1]):
        if (a.y > point.y) != (b.y > point.y):
            x = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x
            if point.x < x:
                inside = not inside
    return inside


def contour_offsets(points, contours):
    depth = sum(contains(other, points[0]) for other in contours if other is not points)
    winding = 1.0 if contour_area(points) > 0 else -1.0
    direction = winding * (-1.0 if depth % 2 else 1.0)
    offsets = []
    for i, point in enumerate(points):
        incoming = (point - points[(i - 1) % len(points)]).normalized()
        outgoing = (points[(i + 1) % len(points)] - point).normalized()
        n1 = Vector((-incoming.y, incoming.x)) * direction
        n2 = Vector((-outgoing.y, outgoing.x)) * direction
        denominator = 1.0 + n1.dot(n2)
        offset = (n1 + n2) / denominator if denominator > 0.02 else n2
        if offset.length > MITER_LIMIT:
            offset = offset.normalized() * MITER_LIMIT
        offsets.append(offset)
    return offsets


def safe_rim_width(point, offset, contours):
    ray = offset.normalized()
    nearest = float("inf")
    for contour in contours:
        for a, b in zip(contour, contour[1:] + contour[:1]):
            edge = b - a
            denominator = ray.x * edge.y - ray.y * edge.x
            if abs(denominator) < 1e-9:
                continue
            delta = a - point
            distance = (delta.x * edge.y - delta.y * edge.x) / denominator
            segment = (delta.x * ray.y - delta.y * ray.x) / denominator
            if distance > 1e-5 and -1e-6 <= segment <= 1.000001:
                nearest = min(nearest, distance)
    if math.isfinite(nearest):
        return min(RIM_WIDTH, nearest * 0.45 / max(offset.length, 1e-5))
    return RIM_WIDTH


def outline_contours(name, char, size, collection):
    curve = bpy.data.curves.new(name + "Contour", "FONT")
    curve.body = char
    curve.font = bpy.data.fonts["Georgia Bold"]
    curve.size = size
    obj = bpy.data.objects.new(name + "Contour", curve)
    obj["generator"] = OWNER
    collection.objects.link(obj)
    for selected in list(bpy.context.selected_objects):
        selected.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="CURVE")
    contours = []
    for spline in obj.data.splines:
        points = []
        if spline.type == "BEZIER":
            knots = list(spline.bezier_points)
            for a, b in zip(knots, knots[1:] + knots[:1]):
                p0 = Vector(a.co[:2])
                p1 = Vector(a.handle_right[:2])
                p2 = Vector(b.handle_left[:2])
                p3 = Vector(b.co[:2])
                chord = p3 - p0
                distance = max(abs(chord.x * (p1.y - p0.y) - chord.y * (p1.x - p0.x)), abs(chord.x * (p2.y - p0.y) - chord.y * (p2.x - p0.x))) / max(chord.length, 1e-8)
                count = 4 if distance > 0.0005 else 1
                for i in range(count):
                    t = i / count
                    point = p0 * (1 - t) ** 3 + p1 * (3 * (1 - t) ** 2 * t) + p2 * (3 * (1 - t) * t ** 2) + p3 * t ** 3
                    if not points or (point - points[-1]).length > 1e-6:
                        points.append(point)
        else:
            points = [Vector(point.co[:2]) for point in spline.points]
        if len(points) > 2:
            contours.append(points)
    bpy.data.objects.remove(obj, do_unlink=True)
    expanded = []
    for points in contours:
        offsets = contour_offsets(points, contours)
        expanded.append([point - offset * STROKE_EXPANSION for point, offset in zip(points, offsets)])
    return expanded


def rim_mesh(name, contours, collection):
    vertices = []
    faces = []
    profile = [(0.0, 0.002), (0.0, 0.020), (0.003, 0.0245), (0.006, 0.026), (RIM_WIDTH - 0.006, 0.026), (RIM_WIDTH - 0.003, 0.0245), (RIM_WIDTH, 0.020), (RIM_WIDTH, 0.008)]
    for points in contours:
        offsets = contour_offsets(points, contours)
        widths = [safe_rim_width(point, offset, contours) for point, offset in zip(points, offsets)]
        start = len(vertices)
        count = len(points)
        for width, z in profile:
            vertices += [(point.x + normal.x * width * safe_width / RIM_WIDTH, point.y + normal.y * width * safe_width / RIM_WIDTH, z) for point, normal, safe_width in zip(points, offsets, widths)]
        for level in range(len(profile)):
            next_level = (level + 1) % len(profile)
            for i in range(count):
                j = (i + 1) % count
                faces.append((start + level * count + i, start + level * count + j, start + next_level * count + j, start + next_level * count + i))
    mesh = bpy.data.meshes.new(name + "Rim")
    mesh.from_pydata(vertices, [], faces)
    obj = bpy.data.objects.new(name + "Rim", mesh)
    obj["generator"] = OWNER
    collection.objects.link(obj)
    return obj


def make_glyph(char, state, collection, editable):
    name = "Glyph_U%04X" % ord(char)
    contours = outline_contours(name, char, state["size"], collection)
    rim = rim_mesh(name, contours, collection)
    core = font_mesh(name + "Core", char, state["size"], STROKE_EXPANSION, 0.003, 0.005, collection, editable)
    vertices = []
    faces = []
    face_slots = []
    for obj, slot in [(core, 0), (rim, 1)]:
        start = len(vertices)
        vertices += [(v.co.x, -(v.co.z + obj.location.z), v.co.y) for v in obj.data.vertices]
        faces += [tuple(start + i for i in p.vertices) for p in obj.data.polygons]
        face_slots += [slot] * len(obj.data.polygons)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(bpy.data.materials["ShipNameGlyphFace"])
    mesh.materials.append(bpy.data.materials["ShipNameplateBrass"])
    for poly, slot in zip(mesh.polygons, face_slots):
        poly.material_index = slot
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = polygon.material_index == 1
    mesh.set_sharp_from_angle(angle=math.radians(40.0))
    uv = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        position = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (position.x, position.z)
    glyph = bpy.data.objects.new(name, mesh)
    glyph["generator"] = OWNER
    glyph["codepoint"] = ord(char)
    glyph["visual_only"] = True
    collection.objects.link(glyph)
    glyph.hide_render = True
    for obj in (rim, core):
        bpy.data.objects.remove(obj, do_unlink=True)
    actual = bounds(mesh)
    minimum = [actual[0][0], actual[2][0], actual[1][0]]
    maximum = [actual[0][1], actual[2][1], actual[1][1]]
    entry = {"codepoint": ord(char), "meshName": name, "advance": state["advances"][state["mapping"][str(ord(char))]] * state["unit"], "width": actual[0][1] - actual[0][0], "boundsMin": minimum, "boundsMax": maximum, "triangles": len(mesh.polygons)}
    glyph["advance"] = entry["advance"]
    return entry


def batch(limit=16):
    scene = generation_scene()
    previous = bpy.context.window.scene
    previous_filepath = bpy.data.filepath
    bpy.context.window.scene = scene
    collection = owned_collection("ShipNameGlyphLibrary", scene)
    editable = owned_collection("ShipNameGlyphEditable", scene)
    state = json.loads(STATE.read_text(encoding="utf-8"))
    payload = json.loads(METRICS.read_text(encoding="utf-8"))
    start = state["next"]
    end = min(start + limit, len(CHARS))
    started = time.monotonic()
    try:
        for i in range(start, end):
            payload["glyphs"].append(make_glyph(CHARS[i], state, collection, editable))
            state["next"] = i + 1
        METRICS.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        STATE.write_text(json.dumps(state), encoding="utf-8")
    finally:
        bpy.context.window.scene = previous
    return {"created": end - start, "completed": state["next"], "total": len(CHARS), "seconds": time.monotonic() - started, "restored_scene": previous.name, "current_filepath_preserved": bpy.data.filepath == previous_filepath}


def export_glyph_library(scene, collection):
    export_collection = owned_collection("ShipNameGlyphExport", scene)
    renamed_objects = []
    renamed_meshes = []
    export_objects = []
    export_path = EXPORT / "ShipNameGlyphs.fbx"
    try:
        for selected in list(bpy.context.selected_objects):
            selected.select_set(False)
        for obj in collection.objects:
            desired_name = "Glyph_U%04X" % obj["codepoint"]
            renamed_objects.append((obj, obj.name))
            obj.name = desired_name + "_Authoring"
            mesh_with_export_name = bpy.data.meshes.get(desired_name)
            if mesh_with_export_name:
                renamed_meshes.append((mesh_with_export_name, mesh_with_export_name.name))
                mesh_with_export_name.name = desired_name + "_AuthoringMesh"
            mesh = obj.data.copy()
            mesh.name = desired_name
            for vertex in mesh.vertices:
                vertex.co.x = -vertex.co.x
                vertex.co.y = -vertex.co.y
            mesh.update()
            copy = bpy.data.objects.new(desired_name, mesh)
            copy["generator"] = OWNER
            export_collection.objects.link(copy)
            export_objects.append(copy)
            copy.select_set(True)
        bpy.context.view_layer.objects.active = export_objects[0]
        bpy.context.view_layer.update()
        bpy.ops.export_scene.fbx(filepath=str(export_path), use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", bake_space_transform=True, axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False, path_mode="AUTO", embed_textures=False)
    finally:
        for obj in export_objects:
            mesh = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)
        for mesh, name in renamed_meshes:
            mesh.name = name
        for obj, name in renamed_objects:
            obj.name = name
    return export_path


def preview_and_export():
    scene = generation_scene()
    previous = bpy.context.window.scene
    previous_filepath = bpy.data.filepath
    bpy.context.window.scene = scene
    collection = owned_collection("ShipNameGlyphLibrary", scene)
    preview = owned_collection("ShipNameGlyphPreview", scene)
    for obj in list(preview.objects):
        if obj.get("generator") == OWNER:
            bpy.data.objects.remove(obj, do_unlink=True)
    payload = json.loads(METRICS.read_text(encoding="utf-8"))
    if len(payload["glyphs"]) != len(CHARS):
        raise RuntimeError("The glyph library is incomplete")
    glyph_map = {entry["codepoint"]: entry for entry in payload["glyphs"]}
    kern_map = {(p["left"], p["right"]): p["offset"] for p in payload["kerning"]}
    phrase = "Чёрная жемчужина"
    positioned = []
    pen = 0.0
    prior = None
    minimum = Vector((1000.0, 1000.0))
    maximum = Vector((-1000.0, -1000.0))
    for char in phrase:
        codepoint = ord(char)
        if prior is not None:
            pen += kern_map.get((prior, codepoint), 0.0)
        if char == " ":
            pen += payload["spaceAdvance"]
        else:
            entry = glyph_map[codepoint]
            positioned.append((entry, pen))
            minimum.x = min(minimum.x, pen + entry["boundsMin"][0])
            maximum.x = max(maximum.x, pen + entry["boundsMax"][0])
            minimum.y = min(minimum.y, entry["boundsMin"][1])
            maximum.y = max(maximum.y, entry["boundsMax"][1])
            pen += entry["advance"]
        prior = codepoint
    height = maximum.y - minimum.y
    horizontal_scale = 0.88
    scale = min(4.2 / ((maximum.x - minimum.x) * horizontal_scale), 0.48 / height, 0.48)
    center_x = (minimum.x + maximum.x) / 2
    center_y = (minimum.y + maximum.y) / 2
    for i, (entry, x) in enumerate(positioned):
        obj = bpy.data.objects.new("PreviewNameGlyph%02d" % i, bpy.data.objects[entry["meshName"]].data)
        obj["generator"] = OWNER
        obj.scale = (scale * horizontal_scale, scale, scale)
        obj.location = ((x - center_x) * scale * horizontal_scale, -0.0006, -center_y * scale)
        preview.objects.link(obj)
    board = bpy.data.collections.get("ShipNameplateModel")
    if board is None:
        raise RuntimeError("Approved nameplate collection is unavailable")
    for obj in board.objects:
        copy = obj.copy()
        copy.name = "GlyphPreview_" + obj.name
        copy["generator"] = OWNER
        preview.objects.link(copy)
    original_scene = bpy.data.scenes.get("ShipNameplateAuthoring")
    for obj in bpy.data.collections["ShipNameplatePreview"].objects:
        copy = obj.copy()
        copy.name = "GlyphPreview_" + obj.name
        copy["generator"] = OWNER
        preview.objects.link(copy)
        if copy.type == "CAMERA":
            copy.data = obj.data.copy()
            scene.camera = copy
    scene.world = original_scene.world
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 610
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.context.view_layer.update()
    export_path = export_glyph_library(scene, collection)
    source_path = SOURCE / "ShipNameGlyphs.blend"
    bpy.data.libraries.write(str(source_path), {scene}, path_remap="RELATIVE", fake_user=True, compress=True)
    preview_path = SOURCE / "ShipNameGlyphsPreview.png"
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(preview_path)
    bpy.ops.render.render(write_still=True, scene=scene.name)
    bpy.context.window.scene = previous
    return {"export": str(export_path), "source": str(source_path), "metrics": str(METRICS), "preview": str(preview_path), "glyphs": len(payload["glyphs"]), "triangles": sum(p["triangles"] for p in payload["glyphs"]), "exampleWidth": (maximum.x - minimum.x) * scale * horizontal_scale, "exampleHeight": height * scale, "exampleCapHeight": scale, "horizontalScale": horizontal_scale, "materials": payload["materialNames"], "restored_scene": previous.name, "current_filepath_preserved": bpy.data.filepath == previous_filepath}


def run(action, count=16):
    if action == "prepare":
        return prepare()
    if action == "batch":
        return batch(count)
    if action == "finish":
        return preview_and_export()
    if action == "export":
        scene = generation_scene()
        previous = bpy.context.window.scene
        previous_filepath = bpy.data.filepath
        bpy.context.window.scene = scene
        try:
            path = export_glyph_library(scene, bpy.data.collections["ShipNameGlyphLibrary"])
        finally:
            bpy.context.window.scene = previous
        glyph = bpy.data.objects["Glyph_U0048"]
        return {"export": str(path), "glyphs": len(bpy.data.collections["ShipNameGlyphLibrary"].objects), "authoring_H_bounds": bounds(glyph.data), "restored_scene": previous.name, "current_filepath_preserved": bpy.data.filepath == previous_filepath}
    raise ValueError(action)
