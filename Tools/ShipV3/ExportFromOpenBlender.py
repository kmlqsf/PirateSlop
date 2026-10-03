import bpy
import json
import math
import hashlib
from pathlib import Path
from mathutils import Vector, Matrix

BASE = Path('D:/projects/Pirate_BR')
OUT = BASE / 'PirateGame/Assets/Models/Ships/ShipV3'
SOURCE = BASE / 'NewShip/V3Preparation'
scene = bpy.context.scene
window = bpy.context.window
old_layer = window.view_layer
assert scene.name == 'Ship_V3_ProportionStudy'
assert bpy.context.mode == 'OBJECT'
root = scene.objects['Ship_V3_Fitted_Root']
progress = json.loads((SOURCE / 'DestructionProgress.json').read_text(encoding='utf-8'))
assert progress['processed'] == len(progress['queue'])
OUT.mkdir(parents=True, exist_ok=True)
(OUT / 'Textures').mkdir(exist_ok=True)
created_objects = []
created_meshes = []
created_groups = []
renamed = {}
copies = {}
original_values = {}
temporary_scene = None

def val(value):
    if isinstance(value, (str, bool, int, float)):
        return value
    if hasattr(value, 'to_dict'):
        return {str(k): val(v) for k, v in value.to_dict().items()}
    if hasattr(value, 'to_list'):
        return [val(v) for v in value.to_list()]
    return str(value)

def under(obj):
    while obj is not None:
        if obj == root:
            return True
        obj = obj.parent
    return False

def update():
    for obj, key in original_values:
        obj.update_tag(refresh={'OBJECT'})
    scene.frame_set(scene.frame_current)
    window.view_layer.update()

def pose(obj):
    graph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(graph)
    parent = obj.parent.evaluated_get(graph).matrix_world if obj.parent else Matrix.Identity(4)
    local = parent.inverted() @ evaluated.matrix_world
    position, rotation, scale = local.decompose()
    return {'position': list(position), 'rotation': list(rotation), 'scale': list(scale),
            'parent_basis': [list(row) for row in parent]}

def mesh_for(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True,
                                         depsgraph=bpy.context.evaluated_depsgraph_get())
    created_meshes.append(mesh)
    return mesh

def realize(obj):
    for modifier in obj.modifiers:
        if modifier.type != 'NODES' or modifier.node_group is None:
            continue
        group = modifier.node_group.copy()
        created_groups.append(group)
        modifier.node_group = group
        output = next(n for n in group.nodes if n.type == 'GROUP_OUTPUT' and n.is_active_output)
        socket = next(s for s in output.inputs if s.type == 'GEOMETRY')
        if socket.is_linked:
            before = socket.links[0].from_socket
            node = group.nodes.new('GeometryNodeRealizeInstances')
            group.links.new(before, node.inputs['Geometry'])
            group.links.new(node.outputs['Geometry'], socket)

def texture(image):
    token = hashlib.sha1(image.name.encode()).hexdigest()[:12]
    suffix = Path(bpy.path.abspath(image.filepath)).suffix.lower()
    suffix = suffix if suffix in ('.png', '.jpg', '.jpeg', '.tga', '.exr') else '.png'
    path = OUT / 'Textures' / (token + suffix)
    if not path.exists():
        if image.packed_file:
            path.write_bytes(image.packed_file.data)
        elif Path(bpy.path.abspath(image.filepath)).is_file():
            import shutil
            shutil.copy2(bpy.path.abspath(image.filepath), path)
        else:
            raise RuntimeError('Texture is unavailable: ' + image.name)
    return 'Assets/Models/Ships/ShipV3/Textures/' + path.name

def image_nodes(socket, visited=None):
    visited = visited or set()
    result = []
    for link in socket.links:
        node = link.from_node
        if node in visited:
            continue
        visited.add(node)
        if node.type == 'TEX_IMAGE' and node.image:
            result.append(node.image)
        else:
            for input_socket in node.inputs:
                if input_socket.is_linked:
                    result.extend(image_nodes(input_socket, visited))
    return result

def bake_material_color(material):
    saved_scene = bpy.context.window.scene
    saved_layer = bpy.context.window.view_layer
    bake_scene = bpy.data.scenes.new('Unity_BaseColor_Temporary')
    mesh = bpy.data.meshes.new('Unity_BaseColor_Plane')
    mesh.from_pydata([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)], [], [(0, 1, 2, 3)])
    copy = material.copy()
    obj = bpy.data.objects.new('Unity_BaseColor_Plane', mesh)
    bake_scene.collection.objects.link(obj)
    mesh.materials.append(copy)
    uv_names = {node.uv_map for node in copy.node_tree.nodes if node.type == 'UVMAP' and node.uv_map} or {'GameUV'}
    for name in uv_names:
        uv = mesh.uv_layers.new(name=name)
        for loop in mesh.loops:
            uv.data[loop.index].uv = mesh.vertices[loop.vertex_index].co.xy
    image = bpy.data.images.new('Unity_BaseColor_Temporary', width=2048, height=2048, alpha=True)
    try:
        image.colorspace_settings.name = 'sRGB'
        nodes = copy.node_tree.nodes
        links = copy.node_tree.links
        shader = next(node for node in nodes if node.type == 'BSDF_PRINCIPLED')
        output = next(node for node in nodes if node.type == 'OUTPUT_MATERIAL' and node.is_active_output)
        emission = nodes.new('ShaderNodeEmission')
        color = shader.inputs['Base Color']
        if color.is_linked:
            links.new(color.links[0].from_socket, emission.inputs['Color'])
        else:
            emission.inputs['Color'].default_value = color.default_value
        links.new(emission.outputs[0], output.inputs['Surface'])
        target = nodes.new('ShaderNodeTexImage')
        target.image = image
        nodes.active = target
        bpy.context.window.scene = bake_scene
        bake_scene.render.engine = 'CYCLES'
        bake_scene.cycles.device = 'CPU'
        bake_scene.cycles.samples = 1
        bake_scene.render.bake.margin = 0
        bake_scene.view_layers[0].objects.active = obj
        obj.select_set(True)
        status = bpy.ops.object.bake(type='EMIT', use_clear=True)
        if 'FINISHED' not in status:
            raise RuntimeError('Base color bake failed: ' + material.name)
        path = OUT / 'Textures' / (material.name + '_FinalBaseColor.png')
        image.filepath_raw = str(path)
        image.file_format = 'PNG'
        image.save()
        return 'Assets/Models/Ships/ShipV3/Textures/' + path.name
    finally:
        bpy.context.window.scene = saved_scene
        bpy.context.window.view_layer = saved_layer
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.meshes.remove(mesh)
        bpy.data.materials.remove(copy)
        bpy.data.images.remove(image)
        bpy.data.scenes.remove(bake_scene)

def material_record(material):
    shader = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if material.use_nodes else None
    record = {'name': material.name, 'color': list(material.diffuse_color), 'metallic': material.metallic,
              'roughness': material.roughness, 'textures': []}
    channel_images = {}
    if shader:
        record['color'] = list(shader.inputs['Base Color'].default_value)
        record['metallic'] = shader.inputs['Metallic'].default_value
        record['roughness'] = shader.inputs['Roughness'].default_value
        for channel in ['Base Color', 'Metallic', 'Roughness', 'Normal', 'Emission Color']:
            images = image_nodes(shader.inputs[channel])
            if channel == 'Base Color':
                images.sort(key=lambda image: 0 if 'albedo' in image.name.lower() else 1 if 'basecolor' in image.name.lower() else 2)
            if images:
                channel_images[channel] = images[0]
                path = bake_material_color(material) if channel == 'Base Color' and 'V19_Preserve_Original_Metal' in material.node_tree.nodes else texture(images[0])
                record['textures'].append({'channel': channel, 'path': path})
                if channel == 'Base Color':
                    record['color'] = [1, 1, 1, 1]
    if 'Metallic' in channel_images or 'Roughness' in channel_images:
        import numpy as np
        packed = np.ones((1024, 1024, 4), dtype=np.float32)
        for channel, index in [('Metallic', 0), ('Roughness', 3)]:
            image = channel_images.get(channel)
            if image:
                width, height = image.size
                pixels = np.empty(width * height * 4, dtype=np.float32)
                image.pixels.foreach_get(pixels)
                pixels = pixels.reshape(height, width, 4)
                values = pixels[np.arange(1024) * height // 1024][:, np.arange(1024) * width // 1024, 0]
            else:
                values = record['metallic' if channel == 'Metallic' else 'roughness']
            packed[:, :, index] = 1 - values if channel == 'Roughness' else values
        image = bpy.data.images.new('Unity_PBR_Pack_Temporary', width=1024, height=1024, alpha=True)
        try:
            image.colorspace_settings.name = 'Non-Color'
            image.pixels.foreach_set(packed.ravel())
            output = OUT / 'Textures' / (material.name + '_MetallicSmoothness.png')
            image.filepath_raw = str(output)
            image.file_format = 'PNG'
            image.save()
            record['textures'].append({'channel': 'MetallicGloss', 'path': 'Assets/Models/Ships/ShipV3/Textures/' + output.name})
        finally:
            bpy.data.images.remove(image)
    return record

try:
    window.view_layer = scene.view_layers['Ship_Edit']
    update()
    originals = {obj.name: obj for obj in scene.objects if under(obj)}
    intact_names = {row['intact'] for row in progress['queue']}
    fragment_names = {name for row in progress['queue'] for name in row['fragment_names']}
    geometry = {name for name, obj in originals.items()
                if obj.type in {'MESH', 'CURVE', 'SURFACE', 'FONT'} and obj.visible_get()
                and not obj.hide_render and not obj.get('exclude_from_export')}
    assert intact_names <= geometry, sorted(intact_names - geometry)[:10]
    names = geometry | fragment_names
    for name in list(names):
        parent = originals[name].parent
        while parent:
            names.add(parent.name)
            parent = parent.parent
    names.update(name for name, obj in originals.items() if obj.type == 'EMPTY' and obj.visible_get())
    materials = []
    used_materials = {mat for name in geometry | fragment_names for mat in getattr(originals[name].data, 'materials', []) if mat}
    for material in sorted(used_materials, key=lambda m: m.name):
        materials.append(material_record(material))
    object_records = []
    for name in sorted(names):
        obj = originals[name]
        points = [obj.matrix_world @ Vector(v) for v in obj.bound_box] if obj.type != 'EMPTY' else [obj.matrix_world.translation]
        object_records.append({'name': name, 'parent': obj.parent.name if obj.parent else '',
                               'bounds_min': [min(p[i] for p in points) for i in range(3)],
                               'bounds_max': [max(p[i] for p in points) for i in range(3)],
                               'properties': [{'key': k, 'value': val(v)} for k, v in obj.items()],
                               'geometry': name in geometry, 'fragment': name in fragment_names})
    sail_samples = []
    tags = ['Main_Course', 'Main_Topsail', 'Fore_Course', 'Fore_Topsail', 'Mizzen']
    for index, tag in enumerate(tags):
        control = originals['V3_Transfer_Control_' + tag]
        original_values[(control, 'deploy')] = control['deploy']
        driven = [obj for name, obj in originals.items() if name in names and tag in name and
                  obj.animation_data and obj.animation_data.drivers]
        samples_by_object = {obj: [] for obj in driven}
        for amount in [step / 16 for step in range(17)]:
            control['deploy'] = amount
            update()
            for obj in driven:
                samples_by_object[obj].append(pose(obj))
        for obj in driven:
            sail_samples.append({'index': index, 'name': obj.name, 'samples': samples_by_object[obj]})
        control['deploy'] = original_values[(control, 'deploy')]
        update()
    anchor_control = originals['V9_AnchorSystem_Control']
    original_values[(anchor_control, 'paid_out_m')] = anchor_control['paid_out_m']
    anchor_samples = []
    anchor_driven = [obj for name, obj in originals.items() if name in names and name.startswith('V9_') and obj.animation_data and obj.animation_data.drivers]
    anchor_poses = {obj: [] for obj in anchor_driven}
    for amount in [step / 4 for step in range(25)]:
        anchor_control['paid_out_m'] = amount
        update()
        for obj in anchor_driven:
            anchor_poses[obj].append(pose(obj))
    for obj in anchor_driven:
        anchor_samples.append({'index': -1, 'name': obj.name, 'samples': anchor_poses[obj]})
    anchor_control['paid_out_m'] = original_values[(anchor_control, 'paid_out_m')]
    update()
    for obj, key in original_values:
        obj[key] = 1 if key == 'deploy' else 0
    update()
    graph = bpy.context.evaluated_depsgraph_get()
    matrices = {name: originals[name].evaluated_get(graph).matrix_world.copy() for name in names}
    parents = {name: originals[name].parent.name if originals[name].parent else '' for name in names}
    temporary_scene = bpy.data.scenes.new('UnityV3_Export_Temporary')
    for name in sorted(names):
        obj = originals[name]
        copy = obj.copy()
        copy.animation_data_clear()
        if obj.data:
            copy.data = obj.data.copy()
            if copy.type == 'MESH':
                created_meshes.append(copy.data)
                if copy.data.shape_keys:
                    copy.data.shape_keys.animation_data_clear()
            elif copy.type != 'EMPTY':
                pass
        temporary_scene.collection.objects.link(copy)
        created_objects.append(copy)
        copy.hide_viewport = copy.hide_render = copy.hide_select = False
        realize(copy)
        copies[name] = copy
    for name, obj in copies.items():
        obj.parent = copies.get(parents[name])
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.matrix_world = matrices[name]
    for name in sorted(names):
        original = originals[name]
        original.name = '__UnityV3_Source_' + name
        renamed[name] = original
        copies[name].name = name
    window.scene = temporary_scene
    temporary_scene.view_layers[0].update()
    for name, obj in list(copies.items()):
        if obj.type in {'CURVE', 'SURFACE', 'FONT'} or any(m.type == 'NODES' for m in obj.modifiers):
            mesh = mesh_for(obj)
            assert len(mesh.vertices) > 0, 'Empty evaluated geometry: ' + name
            if obj.type != 'MESH':
                replacement = bpy.data.objects.new(name + '_Converted', mesh)
                temporary_scene.collection.objects.link(replacement)
                created_objects.append(replacement)
                replacement.parent = obj.parent
                replacement.matrix_world = obj.matrix_world.copy()
                for child in list(obj.children):
                    matrix = child.matrix_world.copy()
                    child.parent = replacement
                    child.matrix_world = matrix
                obj.name = '__UnityV3_OldCurve_' + name
                replacement.name = name
                bpy.data.objects.remove(obj, do_unlink=True)
                created_objects.remove(obj)
                copies[name] = replacement
            else:
                obj.modifiers.clear()
                obj.data = mesh
    evaluated_materials = {mat for obj in copies.values() for mat in getattr(obj.data, 'materials', []) if mat}
    for material in sorted(evaluated_materials - used_materials, key=lambda m: m.name):
        materials.append(material_record(material))
    for name, position in [('UnityV3_Origin', (0, 0, 0)), ('UnityV3_Right', (1, 0, 0)),
                           ('UnityV3_Up', (0, 0, 1)), ('UnityV3_Forward', (0, 1, 0))]:
        obj = bpy.data.objects.new(name, None)
        temporary_scene.collection.objects.link(obj)
        obj.location = position
        created_objects.append(obj)
    for obj in temporary_scene.objects:
        obj.select_set(True)
    temporary_scene.view_layers[0].objects.active = copies[root.name] if root.name in copies else copies['Ship_V3_Fitted_Root']
    status = bpy.ops.export_scene.fbx(filepath=str(OUT / 'ShipV3.fbx'), check_existing=False,
                                    use_selection=True, object_types={'EMPTY', 'MESH'},
                                    global_scale=1, apply_unit_scale=False, axis_forward='-Z', axis_up='Y',
                                    use_mesh_modifiers=False, bake_anim=False, use_custom_props=False,
                                    path_mode='STRIP', mesh_smooth_type='OFF')
    assert 'FINISHED' in status
    metadata = {'source': bpy.data.filepath, 'waterline': 4.6, 'objects': object_records,
                'materials': materials, 'pieces': progress['queue'], 'sail_samples': sail_samples,
                'anchor_samples': anchor_samples,
                'attachments': json.loads((SOURCE / 'V18AttachmentSupports.json').read_text(encoding='utf-8')),
                'structure': json.loads((SOURCE / 'V18WoodSupportGraph.json').read_text(encoding='utf-8'))}
    (OUT / 'ShipV3.json').write_text(json.dumps(metadata, ensure_ascii=False), encoding='utf-8')
    print(json.dumps({'export': str(OUT / 'ShipV3.fbx'), 'objects': len(copies),
                      'pieces': len(progress['queue']), 'fragments': len(fragment_names),
                      'materials': len(materials), 'source_saved': False}))
except Exception:
    import traceback
    (OUT / 'ExportError.txt').write_text(traceback.format_exc(), encoding='utf-8')
    raise
finally:
    window.scene = scene
    window.view_layer = old_layer
    for obj, key in original_values:
        obj[key] = original_values[(obj, key)]
    for obj in created_objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for name, obj in renamed.items():
        obj.name = name
    for mesh in created_meshes:
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for group in created_groups:
        if group.users == 0:
            bpy.data.node_groups.remove(group)
    if temporary_scene:
        bpy.data.scenes.remove(temporary_scene)
    update()
