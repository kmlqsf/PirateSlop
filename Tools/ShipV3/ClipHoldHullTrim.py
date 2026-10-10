import bpy
import json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

base = Path('D:/projects/Pirate_BR/PirateGame')
folder = base / 'Assets/Models/Ships/ShipV3/Bilge'
manifest = json.loads((folder / 'BilgeDeck.json').read_text(encoding='utf-8'))
document = json.loads((folder.parent / 'ShipV3.json').read_text(encoding='utf-8'))
trim_ids = manifest.get('hull_trim_sections') or [10000 + p['piece_id'] for p in document['pieces'] if p['intact'].startswith(('V3_Hull_Wale_L2_', 'V3_Hull_Wale_L3_'))]
manifest['hull_trim_sections'] = sorted(trim_ids)
pieces = [p for p in document['pieces'] if 10000 + p['piece_id'] in trim_ids]
names = [name for p in pieces for name in [p['intact']] + p['fragment_names']]
assert bpy.data.objects['Ship_V3_Fitted_Root'].get('bilge_deck_version') == 6

vertices = []
triangles = []
for p in document['pieces']:
    if p['family'] != 'Hull':
        continue
    obj = bpy.data.objects[p['intact']]
    obj.data.calc_loop_triangles()
    offset = len(vertices)
    vertices.extend(obj.matrix_world @ v.co for v in obj.data.vertices)
    triangles.extend(tuple(offset + index for index in triangle.vertices) for triangle in obj.data.loop_triangles)
hull = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)

def field(point):
    sign = -1 if point.x < 0 else 1
    hit = hull.ray_cast(Vector((sign * 12, point.y, point.z)), Vector((-sign, 0, 0)), 12)[0]
    if hit is None:
        hits = [hull.ray_cast(Vector((sign * 12, point.y + dy, point.z + dz)), Vector((-sign, 0, 0)), 12)[0] for dy, dz in ((0, -.02), (0, .02), (-.02, 0), (.02, 0))]
        hit = next((candidate for candidate in hits if candidate is not None), None)
    return 1 if hit is None else abs(point.x) - abs(hit.x) + .008

modified = set(manifest['modified_objects'])
empty = set(manifest['empty_objects'])
old_faces = new_faces = 0
for name in names:
    obj = bpy.data.objects[name]
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv_names = [layer.name for layer in mesh.uv_layers]
    positions = [obj.matrix_world @ vertex.co for vertex in mesh.vertices]
    values = [field(point) for point in positions]
    inverse = obj.matrix_world.inverted()
    output_vertices, output_faces, material_ids, smoothing = [], [], [], []
    output_uvs = [[] for layer in uv_names]
    old_faces += len(mesh.loop_triangles)
    for triangle in mesh.loop_triangles:
        polygon = [(positions[index], [mesh.uv_layers[layer].data[loop].uv.copy() for layer in uv_names], values[index]) for index, loop in zip(triangle.vertices, triangle.loops)]
        clipped = []
        previous = polygon[-1]
        for current in polygon:
            if (current[2] >= 0) != (previous[2] >= 0):
                lo, hi = 0., 1.
                for iteration in range(12):
                    mid = (lo + hi) * .5
                    if (field(previous[0].lerp(current[0], mid)) >= 0) == (previous[2] >= 0):
                        lo = mid
                    else:
                        hi = mid
                t = (lo + hi) * .5
                clipped.append((previous[0].lerp(current[0], t), [a.lerp(b, t) for a, b in zip(previous[1], current[1])], 0))
            if current[2] >= 0:
                clipped.append(current)
            previous = current
        for vertex in range(2, len(clipped)):
            face = [clipped[0], clipped[vertex - 1], clipped[vertex]]
            offset = len(output_vertices)
            output_vertices.extend(inverse @ item[0] for item in face)
            output_faces.append((offset, offset + 1, offset + 2))
            material_ids.append(triangle.material_index)
            smoothing.append(mesh.polygons[triangle.polygon_index].use_smooth)
            for layer in range(len(uv_names)):
                output_uvs[layer].extend(item[1][layer] for item in face)
    output = bpy.data.meshes.new(name + '_Exterior')
    output.from_pydata(output_vertices, [], output_faces)
    for material in mesh.materials:
        output.materials.append(material)
    for polygon, material, smooth in zip(output.polygons, material_ids, smoothing):
        polygon.material_index = material
        polygon.use_smooth = smooth
    for layer, uvs in zip(uv_names, output_uvs):
        destination = output.uv_layers.new(name=layer)
        for loop, uv in zip(destination.data, uvs):
            loop.uv = uv
    output.update()
    obj.data = output
    new_faces += len(output_faces)
    modified.add(name)
    if not output_faces:
        empty.add(name)
manifest['version'] = 7
manifest['modified_objects'] = sorted(modified)
manifest['empty_objects'] = sorted(empty)
(folder / 'BilgeDeck.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
bpy.data.objects['Ship_V3_Fitted_Root']['bilge_deck_version'] = 7
bpy.context.view_layer.update()
result = {'objects': len(names), 'triangles_before': old_faces, 'triangles_after': new_faces, 'empty': len(empty)}
