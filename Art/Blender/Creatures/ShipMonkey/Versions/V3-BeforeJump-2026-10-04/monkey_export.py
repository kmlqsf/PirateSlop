import bpy, os, json

folder=r'C:\Users\K\Project\Art\Blender\Creatures\ShipMonkey'
model_folder=r'C:\Users\K\Project\Assets\Models\Creatures\ShipMonkey'
os.makedirs(folder,exist_ok=True)
rig=bpy.data.objects['ShipMonkeyRig'];mesh=bpy.data.objects['ShipMonkeyMesh']
scene=bpy.context.scene
legacy=[a for a in bpy.data.actions if a.name.startswith('Legacy')]
if len(legacy)!=5: raise RuntimeError('Preserve all five original animation clips before exporting.')
rig.location=(0,0,0);rig.rotation_euler=(0,0,0)
rig.animation_data.action=bpy.data.actions['Idle']
scene.frame_start=1;scene.frame_end=90;scene.frame_set(1)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(folder,'ShipMonkey.blend'),check_existing=False)
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=os.path.join(model_folder,'ShipMonkeyRigged.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='STRIP',use_tspace=True)
result={'blend':os.path.join(folder,'ShipMonkey.blend'),'fbx':os.path.join(model_folder,'ShipMonkeyRigged.fbx'),'clips':[{ 'name':a.name,'range':list(a.frame_range)} for a in bpy.data.actions]}
