from pathlib import Path
import bpy
from mathutils import Vector

root = Path(__file__).resolve().parents[3]
previous_scene = bpy.context.window.scene
source = root / 'Art/Blender/Kraken/TripoSources'
scene = bpy.data.scenes.get('KrakenTripoWork')
if scene is None:
    scene = bpy.data.scenes.new('KrakenTripoWork')
bpy.context.window.scene = scene
meshes = [o for o in scene.objects if o.type == 'MESH']
if not meshes:
    bpy.ops.import_scene.fbx(filepath=str(source / 'TentacleSource.fbx'))
    meshes = [o for o in scene.objects if o.type == 'MESH']
mesh = meshes[0]
if mesh.name != 'KrakenTentacleMesh':
    points = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    low, high = min(p.z for p in points), max(p.z for p in points)
    base = [p for p in points if p.z < low + (high-low)*.03]
    cx = (min(p.x for p in base)+max(p.x for p in base))*.5
    cy = (min(p.y for p in base)+max(p.y for p in base))*.5
    scale = 24.0/(high-low)
    for v,p in zip(mesh.data.vertices,points):
        v.co = Vector(((p.x-cx)*scale,(p.y-cy)*scale,(p.z-low)*scale))
    mesh.matrix_world.identity()
    mesh.name, mesh.data.name = 'KrakenTentacleMesh', 'KrakenTentacle'
for mat in mesh.data.materials:
    if not mat or not mat.use_nodes:
        continue
    mat.name = 'KrakenTripoSkin'
    for node in mat.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image:
            suffix = node.image.name.rsplit('_',1)[-1].lower()
            candidates = list(source.glob('Tentacle_'+suffix+'.*'))
            if candidates:
                node.image = bpy.data.images.load(str(candidates[0]),check_existing=True)
                if suffix != 'basecolor':
                    node.image.colorspace_settings.name = 'Non-Color'
arm = bpy.data.objects.get('KrakenTentacleRig')
if arm is None:
    arm = bpy.data.objects.new('KrakenTentacleRig',bpy.data.armatures.new('KrakenTentacleRig'))
    scene.collection.objects.link(arm)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    previous = None
    for i in range(17):
        bone = arm.data.edit_bones.new('TentacleBone_'+str(i).zfill(2))
        bone.head, bone.tail = (0,0,24*i/16), (0,0,24*(i+1)/16)
        if previous:
            bone.parent, bone.use_connect = previous, True
        previous = bone
    bpy.ops.object.mode_set(mode='OBJECT')
    mesh.parent = arm
    mod = mesh.modifiers.new('TentacleSkin','ARMATURE')
    mod.object = arm
    groups = [mesh.vertex_groups.new(name='TentacleBone_'+str(i).zfill(2)) for i in range(17)]
    for v in mesh.data.vertices:
        s = max(0,min(16,v.co.z/1.5))
        lower = min(15,int(s))
        t = s-lower
        groups[lower].add([v.index],1-t,'REPLACE')
        if t > 0:
            groups[lower+1].add([v.index],t,'REPLACE')
scene.unit_settings.system, scene.unit_settings.scale_length = 'METRIC', 1
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Art/Blender/Kraken/KrakenTripoRig.blend'),copy=True)
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Models/KrakenTentacles.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,path_mode='STRIP',mesh_smooth_type='FACE',apply_scale_options='FBX_SCALE_UNITS')
result = {'vertices':len(mesh.data.vertices),'triangles':sum(len(p.vertices)-2 for p in mesh.data.polygons),'bones':len(arm.data.bones),'length_m':24,'blend':str(root/'Art/Blender/Kraken/KrakenTripoRig.blend')}
bpy.context.window.scene = previous_scene
