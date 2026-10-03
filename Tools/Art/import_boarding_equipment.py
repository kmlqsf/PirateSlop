import bpy
import json
from pathlib import Path
from mathutils import Vector

project = Path(project_root) if 'project_root' in globals() else Path(__file__).resolve().parents[2]
folder = project / 'Art' / 'Blender' / 'LootReplacement' / 'BoardingEquipment'
manifest = json.loads((folder / 'ImportManifest.json').read_text(encoding='utf-8'))
original_scene = bpy.context.window.scene
original_selected = list(bpy.context.selected_objects)
original_active = bpy.context.view_layer.objects.active
probe = bpy.data.objects.new('CodexBoardingConnectionProbe', None)
original_scene.collection.objects.link(probe)
bpy.data.objects.remove(probe, do_unlink=True)
scene = bpy.data.scenes.new('BoardingEquipmentSource')
objects_before = set(bpy.data.objects)
meshes_before = set(bpy.data.meshes)
materials_before = set(bpy.data.materials)
images_before = set(bpy.data.images)
bpy.context.window.scene = scene
report = []
try:
    for entry in manifest:
        bpy.ops.object.select_all(action='DESELECT')
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=entry['source'])
        objects = [o for o in bpy.data.objects if o not in before]
        collection = bpy.data.collections.new(entry['key'])
        scene.collection.children.link(collection)
        for index, obj in enumerate(objects):
            for parent in list(obj.users_collection):
                parent.objects.unlink(obj)
            collection.objects.link(obj)
            obj.name = entry['key'] + ('Geometry' if len(objects) == 1 else 'Part' + str(index))
            if obj.type == 'MESH':
                obj.data.name = obj.name + 'Mesh'
                material = bpy.data.materials.new(entry['key'] + 'SourceMaterial')
                material.use_nodes = True
                bsdf = next((n for n in material.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
                if bsdf is None:
                    bsdf = material.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
                    output = next(n for n in material.node_tree.nodes if n.type == 'OUTPUT_MATERIAL')
                    material.node_tree.links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
                src = Path(entry['source']).parent
                for suffix, input_name in [('basecolor', 'Base Color'), ('metallic', 'Metallic'), ('roughness', 'Roughness'), ('normal', 'Normal')]:
                    texture_path = next(src.rglob('*_' + suffix + '.*'))
                    image = bpy.data.images.load(str(texture_path), check_existing=True)
                    image.colorspace_settings.name = 'sRGB' if suffix == 'basecolor' else 'Non-Color'
                    image.pack()
                    node = material.node_tree.nodes.new('ShaderNodeTexImage')
                    node.image = image
                    if suffix == 'normal':
                        normal = material.node_tree.nodes.new('ShaderNodeNormalMap')
                        material.node_tree.links.new(node.outputs['Color'], normal.inputs['Color'])
                        material.node_tree.links.new(normal.outputs['Normal'], bsdf.inputs[input_name])
                    else:
                        material.node_tree.links.new(node.outputs['Color'], bsdf.inputs[input_name])
                obj.data.materials.clear()
                obj.data.materials.append(material)
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        vertices = [o.matrix_world @ v.co for o in objects if o.type == 'MESH' for v in o.data.vertices]
        report.append({'key': entry['key'], 'vertices': len(vertices), 'triangles': sum(len(p.vertices)-2 for o in objects if o.type == 'MESH' for p in o.data.polygons), 'bounds_min': [min(v[i] for v in vertices) for i in range(3)], 'bounds_max': [max(v[i] for v in vertices) for i in range(3)]})
        bpy.ops.export_scene.fbx(filepath=entry['output'], use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
    bpy.data.libraries.write(str(folder / 'BoardingEquipment.blend'), {scene}, path_remap='RELATIVE', fake_user=True, compress=True)
    (folder / 'BlenderImportReport.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    result = {'models': report, 'source': str(folder / 'BoardingEquipment.blend'), 'preserved_scene': original_scene.name}
finally:
    bpy.context.window.scene = original_scene
    for obj in list(bpy.data.objects):
        if obj not in objects_before:
            bpy.data.objects.remove(obj, do_unlink=True)
    for child in list(scene.collection.children):
        bpy.data.collections.remove(child)
    bpy.data.scenes.remove(scene)
    for data, before in [(bpy.data.meshes, meshes_before), (bpy.data.materials, materials_before), (bpy.data.images, images_before)]:
        for block in list(data):
            if block not in before and block.users == 0:
                data.remove(block)
    for obj in original_selected:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = original_active
