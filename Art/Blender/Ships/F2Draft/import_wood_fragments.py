import bpy
import json
from pathlib import Path

base = Path('C:/Users/K/Project')
scene = bpy.data.scenes.get('WoodDestructionSource') or bpy.data.scenes.new('WoodDestructionSource')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
payload = json.loads((base / 'Art/Blender/Ships/F2Draft/wood-fragments.json').read_text())
for name in ['F2_WoodFragments', 'F2_WoodFragmentCollision']:
    previous = bpy.data.collections.get(name)
    if previous:
        for obj in list(previous.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(previous)
if not scene.camera:
    camera = bpy.data.objects.new('WoodSourceCamera', bpy.data.cameras.new('WoodSourceCamera'))
    scene.collection.objects.link(camera)
    camera.location = (35, -45, 25)
    from mathutils import Vector
    camera.rotation_euler = (Vector((0, 0, 5)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = camera
if not any(o.type == 'LIGHT' for o in scene.objects):
    light = bpy.data.objects.new('WoodSourceSun', bpy.data.lights.new('WoodSourceSun', 'SUN'))
    scene.collection.objects.link(light)
    light.rotation_euler = (.5, -.5, -.5)
    light.data.energy = 3
visuals = bpy.data.collections.new('F2_WoodFragments')
collisions = bpy.data.collections.new('F2_WoodFragmentCollision')
scene.collection.children.link(visuals)
scene.collection.children.link(collisions)
outer = bpy.data.materials['Mat_StylShip_ShipHull']
fresh = bpy.data.materials['FreshSplitWood']
fresh.use_nodes = True
fresh.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = (.56, .31, .12, 1)
exports = []
collision_exports = []
for section in payload:
    x, y, z = section['position']
    for part in section['parts']:
        name = 'SD%d_%s' % (section['id'], part['name'])
        verts = [(v[0], -v[2], v[1]) for v in part['vertices']]
        faces = []
        indices = []
        for sub, triangles in enumerate(part['triangles']):
            for i in range(0, len(triangles), 3):
                faces.append(tuple(triangles[i:i+3]))
                indices.append(sub)
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        mesh.materials.append(outer)
        mesh.materials.append(fresh)
        for poly, index in zip(mesh.polygons, indices):
            poly.material_index = min(index, 1)
        uv = mesh.uv_layers.new(name='UVMap')
        for loop in mesh.loops:
            if loop.vertex_index < len(part['uv']):
                uv.data[loop.index].uv = part['uv'][loop.vertex_index]
        obj = bpy.data.objects.new(name, mesh)
        obj.location = (x, -z, y)
        obj['SectionId'] = section['id']
        visuals.objects.link(obj)
        exports.append(obj)
        triangles = part['collisionTriangles']
        collision_mesh = bpy.data.meshes.new(name + '_Collision')
        collision_mesh.from_pydata([(v[0], -v[2], v[1]) for v in part['collisionVertices']], [], [tuple(triangles[i:i+3]) for i in range(0, len(triangles), 3)])
        collision = bpy.data.objects.new(name + '_Collision', collision_mesh)
        collision.location = obj.location
        collision.display_type = 'WIRE'
        collisions.objects.link(collision)
        collision_exports.append(collision)
for objects, filename in [(exports, 'MainShipWoodFragments.fbx'), (collision_exports, 'MainShipWoodFragmentsCollision.fbx')]:
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(base / 'Assets/Models/Ships/MainShip/Destruction' / filename), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', add_leaf_bones=False, bake_anim=False)
collisions.hide_viewport = True
collisions.hide_render = True
bpy.ops.wm.save_as_mainfile(filepath=str(base / 'Art/Blender/Ships/F2Draft/F2_Ship_Destruction.blend'))
result = {'sections': len(payload), 'fragments': len(exports), 'saved': bpy.data.filepath}
