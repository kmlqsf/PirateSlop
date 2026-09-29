import array
import math
from pathlib import Path

import bpy
from io_scene_fbx import encode_bin, parse_fbx


def smooth(a, b, value):
    t = max(0.0, min(1.0, (value - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def head_mask(co):
    x, y, z = co
    skull = smooth(.787, .810, y)
    width = .020 + .046 * smooth(.714, .790, y)
    beard = (1.0 - smooth(width, width + .008, abs(x)))
    rear = .035 + (.800 - y) * .50
    beard *= smooth(rear, rear + .008, z) * smooth(.686, .696, y)
    return max(skull, beard)


def child(node, name):
    return next(item for item in node.elems if item.id == name)


def encode_node(node):
    methods = {
        'B': 'bool', 'C': 'char', 'Z': 'int8', 'Y': 'int16',
        'I': 'int32', 'L': 'int64', 'F': 'float32', 'D': 'float64',
        'R': 'bytes', 'S': 'string', 'i': 'int32_array',
        'l': 'int64_array', 'f': 'float32_array', 'd': 'float64_array',
        'b': 'bool_array', 'c': 'byte_array',
    }
    target = encode_bin.FBXElem(node.id)
    for kind, value in zip(node.props_type, node.props):
        getattr(target, 'add_' + methods[chr(kind)])(value)
    target.elems = [encode_node(item) for item in node.elems]
    return target


def assert_same_nodes(before, after, changed_clusters, cluster_id=None, depth=0):
    assert before.id == after.id
    assert before.props_type == after.props_type
    assert len(before.elems) == len(after.elems)
    if before.id == b'Deformer':
        cluster_id = before.props[0]
    mutable = cluster_id in changed_clusters and before.id in (b'Indexes', b'Weights')
    metadata = depth == 1 and before.id in (b'FileId', b'CreationTime')
    if not mutable and not metadata:
        assert len(before.props) == len(after.props)
        for a, b in zip(before.props, after.props):
            if isinstance(a, array.array):
                assert a.tobytes() == b.tobytes(), before.id
            else:
                assert a == b, before.id
    for a, b in zip(before.elems, after.elems):
        assert_same_nodes(a, b, changed_clusters, cluster_id, depth + 1)


def repair(source_fbx, output_fbx, source_blend, scene_name='PirateWeightRepair'):
    scene = bpy.data.scenes[scene_name]
    mesh = next(obj for obj in scene.objects if obj.type == 'MESH')
    rig = next(obj for obj in scene.objects if obj.type == 'ARMATURE')
    original, version = parse_fbx.parse(str(source_fbx))
    root, _ = parse_fbx.parse(str(source_fbx))
    objects = child(root, b'Objects')
    object_ids = {node.props[0]: node for node in objects.elems if node.props}
    geometry = next(node for node in objects.elems if node.id == b'Geometry' and node.props[-1] == b'Mesh')
    coordinates = child(geometry, b'Vertices').props[0]
    assert len(coordinates) == len(mesh.data.vertices) * 3
    assert len(mesh.data.vertices) == 9010
    assert max(abs(coordinates[v.index * 3 + axis] - v.co[axis])
               for v in mesh.data.vertices for axis in range(3)) < 1e-7
    connections = child(root, b'Connections').elems
    clusters = {}
    for node in objects.elems:
        if node.id != b'Deformer' or node.props[-1] != b'Cluster':
            continue
        linked = [object_ids[c.props[1]] for c in connections
                  if c.props[2] == node.props[0] and c.props[1] in object_ids
                  and object_ids[c.props[1]].id == b'Model']
        assert len(linked) == 1
        name = linked[0].props[1].split(b'\x00\x01')[0].decode()
        assert name not in clusters
        indices = child(node, b'Indexes').props[0]
        values = child(node, b'Weights').props[0]
        assert len(indices) == len(values) == len(set(indices))
        clusters[name] = (node, dict(zip(indices, values)))
    head = 'mixamorig:Head'
    assert head in clusters
    changes = {}
    rigid = 0
    for vertex in mesh.data.vertices:
        mask = head_mask(vertex.co)
        weights = {name: data[1][vertex.index] for name, data in clusters.items()
                   if data[1].get(vertex.index, 0.0) > 0.0}
        if mask == 0.0 or weights.get(head, 0.0) >= 1.0 - 1e-7:
            continue
        total = sum(weights.values())
        assert abs(total - 1.0) < 1e-5
        updated = {name: weight * (1.0 - mask) for name, weight in weights.items()}
        updated[head] = updated.get(head, 0.0) + total * mask
        updated = dict(sorted(((name, value) for name, value in updated.items() if value > 1e-8),
                              key=lambda item: item[1], reverse=True)[:4])
        norm = sum(updated.values())
        updated = {name: value / norm for name, value in updated.items()}
        assert all(math.isfinite(value) and value >= 0.0 for value in updated.values())
        assert abs(sum(updated.values()) - 1.0) < 1e-10
        changes[vertex.index] = updated
        rigid += int(updated.get(head) == 1.0)
    assert changes
    changed_clusters = set()
    for name, (node, weights) in clusters.items():
        updated = weights.copy()
        for index, replacement in changes.items():
            updated.pop(index, None)
            if name in replacement:
                updated[index] = replacement[name]
        if updated == weights:
            continue
        changed_clusters.add(node.props[0])
        indices = child(node, b'Indexes')
        values = child(node, b'Weights')
        indices.props[0] = array.array(indices.props[0].typecode, updated)
        values.props[0] = array.array(values.props[0].typecode, updated.values())
    output_fbx = Path(output_fbx)
    output_fbx.parent.mkdir(parents=True, exist_ok=True)
    encode_bin.write(str(output_fbx), encode_node(root), version)
    check, check_version = parse_fbx.parse(str(output_fbx))
    assert check_version == version
    assert_same_nodes(original, check, changed_clusters)
    checked_clusters = {node.props[0]: node for node in child(check, b'Objects').elems
                        if node.id == b'Deformer' and node.props[-1] == b'Cluster'}
    for name, (node, weights) in clusters.items():
        stored = checked_clusters[node.props[0]]
        stored_weights = dict(zip(child(stored, b'Indexes').props[0], child(stored, b'Weights').props[0]))
        assert {i: w for i, w in weights.items() if i not in changes} == {
            i: w for i, w in stored_weights.items() if i not in changes}
        for index, replacement in changes.items():
            assert stored_weights.get(index, 0.0) == replacement.get(name, 0.0)
    indices = list(changes)
    for name in clusters:
        group = mesh.vertex_groups.get(name)
        if group is not None:
            group.remove(indices)
    for index, replacement in changes.items():
        for name, value in replacement.items():
            group = mesh.vertex_groups.get(name) or mesh.vertex_groups.new(name=name)
            group.add([index], value, 'REPLACE')
    mask_colors = mesh.data.color_attributes.get('HeadRepairMask')
    if mask_colors is not None:
        mesh.data.color_attributes.remove(mask_colors)
    scene.display.shading.color_type = 'MATERIAL'
    rig.data.pose_position = 'REST'
    mesh['weight_repair'] = 'Head and beard follow Head; feathered neck boundary. Original geometry, rig and animation retained.'
    bpy.context.window.scene = scene
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    source_blend = Path(source_blend)
    source_blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.data.libraries.write(str(source_blend), {scene}, path_remap='ABSOLUTE', fake_user=True, compress=True)
    return {'changed_vertices': len(changes), 'rigid_head_vertices_changed': rigid,
            'changed_clusters': len(changed_clusters), 'fbx': str(output_fbx),
            'blend': str(source_blend), 'preserved_geometry_rig_animations': True}
