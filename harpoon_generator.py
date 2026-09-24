import bpy
import bmesh
import math
from mathutils import Vector, Euler, Matrix

def clear_existing_harpoon():
    to_remove = []
    harpoon_keywords = [
        "Base_Mount", "Base_Yaw", "Barrel", "Harpoon", "Rope",
        "Clamp", "Ear", "Hoop", "Barb", "Winch", "Rail_Beam", "Crank", "Cheek",
        "Trunnion", "Screw", "Tommy", "Swivel", "Carriage", "Ratchet", "Gear"
    ]
    for obj in bpy.data.objects:
        if any(kw.lower() in obj.name.lower() for kw in harpoon_keywords):
            to_remove.append(obj)
    for obj in to_remove:
        bpy.data.objects.remove(obj, do_unlink=True)
        
    for m in list(bpy.data.meshes):
        if m.users == 0: bpy.data.meshes.remove(m)
    for c in list(bpy.data.curves):
        if c.users == 0: bpy.data.curves.remove(c)

def setup_materials():
    def make_pbr(name, base_color, metallic, roughness):
        mat = bpy.data.materials.get(name)
        if not mat:
            mat = bpy.data.materials.new(name=name)
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        out.location = (300, 0)
        bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
        bsdf.location = (0, 0)
        nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
        
        bsdf.inputs['Base Color'].default_value = base_color
        bsdf.inputs['Metallic'].default_value = metallic
        bsdf.inputs['Roughness'].default_value = roughness
        return mat

    mats = {
        # Warm golden-brown oak wood for ship railing, swivel pedestal & drum face
        'oak': make_pbr("Cannon_Oak", (0.24, 0.11, 0.045, 1.0), metallic=0.0, roughness=0.72),
        # Deep aged dark oak wood for carriage cheek panels
        'oak_dark': make_pbr("Cannon_Oak_Dark", (0.16, 0.075, 0.028, 1.0), metallic=0.0, roughness=0.75),
        # Dark gunmetal forged iron for cannon barrel body
        'iron': make_pbr("Cannon_Iron.001", (0.12, 0.14, 0.16, 1.0), metallic=0.75, roughness=0.32),
        # Forged iron bands, bolts, clamps, cheek trim, and drum rim
        'bands': make_pbr("Cannon_IronBands", (0.15, 0.17, 0.19, 1.0), metallic=0.82, roughness=0.30),
        # Polished sharp steel for harpoon projectile shaft & barbed head
        'steel': make_pbr("Harpoon_Steel", (0.50, 0.54, 0.58, 1.0), metallic=0.90, roughness=0.18),
        # Warm golden-hemp ship rope for drum coils & dynamic line
        'rope': make_pbr("Mat_StylShip_SailsRope", (0.48, 0.35, 0.18, 1.0), metallic=0.0, roughness=0.82),
        # Dark black bore interior
        'bore': make_pbr("Cannon_Bore", (0.012, 0.015, 0.018, 1.0), metallic=0.0, roughness=0.95),
    }
    return mats

def assign_mat(obj, mat):
    if not mat: return
    if obj.data.materials:
        obj.data.materials[0] = mat
    else:
        obj.data.materials.append(mat)

def set_flat_and_bevel(obj, width=0.012, segments=1):
    if hasattr(obj.data, 'shade_flat'):
        obj.data.shade_flat()
    mod = obj.modifiers.new(name="Bevel", type='BEVEL')
    mod.width = width
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(35)

def create_child_cylinder(name, radius, depth, vertices, parent=None, local_loc=(0,0,0), local_rot=(0,0,0), mat=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth)
    obj = bpy.context.active_object
    obj.name = name
    if parent:
        obj.parent = parent
        obj.matrix_parent_inverse.identity()
    obj.location = local_loc
    obj.rotation_euler = local_rot
    if mat: assign_mat(obj, mat)
    obj.data.shade_flat()
    return obj

def create_child_cone(name, radius1, radius2, depth, vertices, parent=None, local_loc=(0,0,0), local_rot=(0,0,0), mat=None):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth)
    obj = bpy.context.active_object
    obj.name = name
    if parent:
        obj.parent = parent
        obj.matrix_parent_inverse.identity()
    obj.location = local_loc
    obj.rotation_euler = local_rot
    if mat: assign_mat(obj, mat)
    obj.data.shade_flat()
    return obj

def create_child_cube(name, size=(1,1,1), parent=None, local_loc=(0,0,0), local_rot=(0,0,0), mat=None):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if parent:
        obj.parent = parent
        obj.matrix_parent_inverse.identity()
    obj.location = local_loc
    obj.rotation_euler = local_rot
    if mat: assign_mat(obj, mat)
    obj.data.shade_flat()
    return obj

def create_child_bolt(name, radius=0.016, depth=0.020, parent=None, local_loc=(0,0,0), local_rot=(0,0,0), mat=None):
    bolt = create_child_cylinder(name, radius=radius, depth=depth, vertices=6, parent=parent, local_loc=local_loc, local_rot=local_rot, mat=mat)
    set_flat_and_bevel(bolt, width=0.003)
    return bolt

def build_harpoon_gun():
    clear_existing_harpoon()
    mats = setup_materials()

    # =========================================================================
    # 1. BASE MOUNT (Base_Mount_Rail - Root at Z = 0)
    # =========================================================================
    # Wooden Railing Beam along Y axis (Length: 1.20m, Width: 0.18m, Height: 0.14m)
    rail_beam = create_child_cube("Rail_Beam_Wood", size=(0.18, 1.20, 0.14), local_loc=(0, 0, -0.07), mat=mats['oak'])
    set_flat_and_bevel(rail_beam, width=0.018)

    # Base_Mount_Rail empty root at Z = 0
    base_mount = bpy.data.objects.new("Base_Mount_Rail", None)
    base_mount.empty_display_type = 'ARROWS'
    base_mount.empty_display_size = 0.25
    bpy.context.collection.objects.link(base_mount)
    base_mount.location = (0, 0, 0)

    # Re-parent rail beam to base mount
    rail_beam.parent = base_mount
    rail_beam.matrix_parent_inverse.identity()
    rail_beam.location = (0, 0, -0.07)

    # Top heavy mounting plate spanning both clamps
    base_plate = create_child_cube("Base_Plate_Iron", size=(0.24, 0.46, 0.035), parent=base_mount, local_loc=(0, 0, 0.0175), mat=mats['bands'])
    set_flat_and_bevel(base_plate, width=0.01)

    # Two Heavy Forged C-Clamps wrapping tightly around the wooden beam
    for idx, y_pos in enumerate([-0.16, 0.16], 1):
        # Top saddle plate
        c_top = create_child_cube(f"Clamp_Top_{idx}", size=(0.23, 0.08, 0.035), parent=base_mount, local_loc=(0, y_pos, 0.018), mat=mats['bands'])
        set_flat_and_bevel(c_top, width=0.008)
        
        # Left leg (front-left in 3/4 view)
        c_left = create_child_cube(f"Clamp_LegLeft_{idx}", size=(0.038, 0.08, 0.19), parent=base_mount, local_loc=(-0.105, y_pos, -0.085), mat=mats['bands'])
        set_flat_and_bevel(c_left, width=0.008)
        
        # Right leg
        c_right = create_child_cube(f"Clamp_LegRight_{idx}", size=(0.038, 0.08, 0.19), parent=base_mount, local_loc=(0.105, y_pos, -0.085), mat=mats['bands'])
        set_flat_and_bevel(c_right, width=0.008)
        
        # Bottom inward clamp jaws/lips
        c_bot_l = create_child_cube(f"Clamp_LipLeft_{idx}", size=(0.055, 0.08, 0.032), parent=base_mount, local_loc=(-0.075, y_pos, -0.185), mat=mats['bands'])
        set_flat_and_bevel(c_bot_l, width=0.008)
        c_bot_r = create_child_cube(f"Clamp_LipRight_{idx}", size=(0.055, 0.08, 0.032), parent=base_mount, local_loc=(0.075, y_pos, -0.185), mat=mats['bands'])
        set_flat_and_bevel(c_bot_r, width=0.008)

        # Hexagonal bolts on the outer clamp face (matching reference: 2 bolts on front face!)
        create_child_bolt(f"Clamp_BoltTop_L_{idx}", radius=0.016, depth=0.022, parent=base_mount, local_loc=(-0.126, y_pos, -0.03), local_rot=(0, -math.pi/2, 0), mat=mats['bands'])
        create_child_bolt(f"Clamp_BoltBot_L_{idx}", radius=0.016, depth=0.022, parent=base_mount, local_loc=(-0.126, y_pos, -0.13), local_rot=(0, -math.pi/2, 0), mat=mats['bands'])
        
        create_child_bolt(f"Clamp_BoltTop_R_{idx}", radius=0.016, depth=0.022, parent=base_mount, local_loc=(0.126, y_pos, -0.03), local_rot=(0, math.pi/2, 0), mat=mats['bands'])
        create_child_bolt(f"Clamp_BoltBot_R_{idx}", radius=0.016, depth=0.022, parent=base_mount, local_loc=(0.126, y_pos, -0.13), local_rot=(0, math.pi/2, 0), mat=mats['bands'])

        # Bottom clamping bolts
        create_child_bolt(f"Clamp_BoltJaws_L_{idx}", radius=0.015, depth=0.022, parent=base_mount, local_loc=(-0.075, y_pos, -0.205), local_rot=(0, 0, 0), mat=mats['bands'])
        create_child_bolt(f"Clamp_BoltJaws_R_{idx}", radius=0.015, depth=0.022, parent=base_mount, local_loc=(0.075, y_pos, -0.205), local_rot=(0, 0, 0), mat=mats['bands'])

    # Central under-beam bridge connecting clamp jaws
    bot_bridge = create_child_cube("Clamp_Bottom_Bridge", size=(0.09, 0.38, 0.03), parent=base_mount, local_loc=(0, 0, -0.19), mat=mats['bands'])
    set_flat_and_bevel(bot_bridge, width=0.008)

    # Heavy threaded clamping screw with clear threads underneath
    clamp_screw = create_child_cylinder("Clamp_Screw", radius=0.022, depth=0.19, vertices=8, parent=base_mount, local_loc=(0, 0, -0.295), mat=mats['bands'])
    for z_off in [-0.23, -0.255, -0.28, -0.305, -0.33, -0.355]:
        create_child_cylinder(f"Screw_Thread_{z_off}", radius=0.028, depth=0.010, vertices=8, parent=base_mount, local_loc=(0, 0, z_off), mat=mats['bands'])

    # Tommy bar cross handle at bottom of screw
    tommy_hub = create_child_cylinder("Tommy_Bar_Hub", radius=0.034, depth=0.038, vertices=8, parent=base_mount, local_loc=(0, 0, -0.395), mat=mats['bands'])
    set_flat_and_bevel(tommy_hub, width=0.006)
    tommy_pin = create_child_cylinder("Tommy_Bar_Pin", radius=0.012, depth=0.22, vertices=6, parent=base_mount, local_loc=(0, 0, -0.395), local_rot=(math.pi/2, 0, 0), mat=mats['iron'])
    create_child_cylinder("Tommy_Cap_F", radius=0.018, depth=0.018, vertices=6, parent=base_mount, local_loc=(0, -0.11, -0.395), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])
    create_child_cylinder("Tommy_Cap_B", radius=0.018, depth=0.018, vertices=6, parent=base_mount, local_loc=(0, 0.11, -0.395), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])

    # =========================================================================
    # 2. BASE YAW (Base_Yaw - Turntable & Cradle Carriage)
    # =========================================================================
    # Fixed bottom collar ring on top of base plate
    yaw_collar_bot = create_child_cylinder("Base_Yaw_CollarBot", radius=0.150, depth=0.035, vertices=8, parent=base_mount, local_loc=(0, 0, 0.045), mat=mats['bands'])
    set_flat_and_bevel(yaw_collar_bot, width=0.01)

    # Base_Yaw Empty Root (Rotates horizontally around Z)
    yaw_angle = math.radians(16)
    base_yaw = bpy.data.objects.new("Base_Yaw", None)
    base_yaw.empty_display_type = 'ARROWS'
    base_yaw.empty_display_size = 0.25
    bpy.context.collection.objects.link(base_yaw)
    base_yaw.parent = base_mount
    base_yaw.matrix_parent_inverse.identity()
    base_yaw.location = (0, 0, 0.045)
    base_yaw.rotation_euler = (0, 0, yaw_angle)

    # Swivel Column: Truncated octagonal wooden pedestal (oak wood tapering upwards, matching reference!)
    swivel_col = create_child_cone("Swivel_Column", radius1=0.138, radius2=0.112, depth=0.08, vertices=8, parent=base_yaw, local_loc=(0, 0, 0.045), mat=mats['oak'])
    set_flat_and_bevel(swivel_col, width=0.01)

    swivel_top_flange = create_child_cylinder("Swivel_Top_Flange", radius=0.150, depth=0.03, vertices=8, parent=base_yaw, local_loc=(0, 0, 0.095), mat=mats['bands'])
    set_flat_and_bevel(swivel_top_flange, width=0.01)

    # Carriage Bed Plate (Base floor of the cradle: Z = 0.11 to 0.14)
    carriage_bed = create_child_cube("Carriage_Bed", size=(0.32, 0.40, 0.035), parent=base_yaw, local_loc=(0, 0, 0.125), mat=mats['bands'])
    set_flat_and_bevel(carriage_bed, width=0.01)

    # Front cross-brace plate connecting the cheeks at the front
    front_brace = create_child_cube("Carriage_Front_Brace", size=(0.28, 0.030, 0.12), parent=base_yaw, local_loc=(0, 0.185, 0.20), mat=mats['bands'])
    set_flat_and_bevel(front_brace, width=0.008)
    create_child_bolt("Front_Brace_Bolt_L", radius=0.012, depth=0.018, parent=base_yaw, local_loc=(-0.11, 0.202, 0.22), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])
    create_child_bolt("Front_Brace_Bolt_R", radius=0.012, depth=0.018, parent=base_yaw, local_loc=(0.11, 0.202, 0.22), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])

    # Carriage Cheeks (Left at local X = -0.14, Right at local X = +0.14)
    # Solid dark oak panels enclosed in forged iron angle trim with corner rivets
    for side, x_pos, name_suffix in [(-1, -0.14, "L"), (1, 0.14, "R")]:
        # Solid dark oak panel (Length 0.36m, height 0.20m)
        cheek_wood = create_child_cube(f"Cheek_Wood_{name_suffix}", size=(0.040, 0.36, 0.20), parent=base_yaw, local_loc=(x_pos, 0, 0.245), mat=mats['oak_dark'])
        set_flat_and_bevel(cheek_wood, width=0.008)

        # Forged iron angle trim framing the wooden panel
        trim_bot = create_child_cube(f"Cheek_TrimBot_{name_suffix}", size=(0.046, 0.38, 0.028), parent=base_yaw, local_loc=(x_pos, 0, 0.155), mat=mats['bands'])
        trim_front = create_child_cube(f"Cheek_TrimFront_{name_suffix}", size=(0.046, 0.028, 0.20), parent=base_yaw, local_loc=(x_pos, 0.175, 0.245), mat=mats['bands'])
        trim_back = create_child_cube(f"Cheek_TrimBack_{name_suffix}", size=(0.046, 0.028, 0.20), parent=base_yaw, local_loc=(x_pos, -0.175, 0.245), mat=mats['bands'])
        trim_top = create_child_cube(f"Cheek_TrimTop_{name_suffix}", size=(0.046, 0.38, 0.028), parent=base_yaw, local_loc=(x_pos, 0, 0.335), mat=mats['bands'])

        # Heavy trunnion bearing cap at the top of each cheek
        trunnion_block = create_child_cube(f"Trunnion_Block_{name_suffix}", size=(0.056, 0.11, 0.038), parent=base_yaw, local_loc=(x_pos, 0, 0.360), mat=mats['bands'])
        set_flat_and_bevel(trunnion_block, width=0.008)

        # Visible corner bolts/rivets on the outer cheek trim (matching reference!)
        create_child_bolt(f"Cheek_Bolt_FL_{name_suffix}", radius=0.012, depth=0.016, parent=base_yaw, local_loc=(x_pos + side*0.025, 0.16, 0.165), local_rot=(0, side*math.pi/2, 0), mat=mats['bands'])
        create_child_bolt(f"Cheek_Bolt_BL_{name_suffix}", radius=0.012, depth=0.016, parent=base_yaw, local_loc=(x_pos + side*0.025, -0.16, 0.165), local_rot=(0, side*math.pi/2, 0), mat=mats['bands'])
        create_child_bolt(f"Cheek_Bolt_MidF_{name_suffix}", radius=0.012, depth=0.016, parent=base_yaw, local_loc=(x_pos + side*0.025, 0.175, 0.245), local_rot=(0, side*math.pi/2, 0), mat=mats['bands'])
        create_child_bolt(f"Cheek_Bolt_MidB_{name_suffix}", radius=0.012, depth=0.016, parent=base_yaw, local_loc=(x_pos + side*0.025, -0.175, 0.245), local_rot=(0, side*math.pi/2, 0), mat=mats['bands'])
        
        # Trunnion cap vertical bolts
        create_child_bolt(f"Trunnion_Bolt1_{name_suffix}", radius=0.012, depth=0.020, parent=base_yaw, local_loc=(x_pos, -0.04, 0.380), local_rot=(0, 0, 0), mat=mats['bands'])
        create_child_bolt(f"Trunnion_Bolt2_{name_suffix}", radius=0.012, depth=0.020, parent=base_yaw, local_loc=(x_pos, 0.04, 0.380), local_rot=(0, 0, 0), mat=mats['bands'])

    # 2.2 Winch / Rope Drum (Mounted on Left Cheek at local Y = -0.09)
    winch_x = -0.25
    winch_y = -0.09
    winch_z = 0.25

    # Support bracket bolted to left cheek
    winch_bracket = create_child_cube("Winch_Bracket", size=(0.06, 0.16, 0.06), parent=base_yaw, local_loc=(-0.17, winch_y, winch_z), mat=mats['bands'])
    set_flat_and_bevel(winch_bracket, width=0.008)

    # Main axle through drum
    winch_axle = create_child_cylinder("Winch_Axle", radius=0.024, depth=0.20, vertices=8, parent=base_yaw, local_loc=(winch_x, winch_y, winch_z), local_rot=(0, math.pi/2, 0), mat=mats['bands'])

    # Inner drum flange
    drum_flange_in = create_child_cylinder("Winch_Flange_In", radius=0.145, depth=0.018, vertices=12, parent=base_yaw, local_loc=(-0.18, winch_y, winch_z), local_rot=(0, math.pi/2, 0), mat=mats['bands'])
    set_flat_and_bevel(drum_flange_in, width=0.01)

    # Outer drum flange: Wooden disc with forged iron outer tire & large central hex nut (matching reference!)
    drum_flange_out_rim = create_child_cylinder("Winch_Flange_Out_Rim", radius=0.145, depth=0.022, vertices=12, parent=base_yaw, local_loc=(-0.31, winch_y, winch_z), local_rot=(0, math.pi/2, 0), mat=mats['bands'])
    set_flat_and_bevel(drum_flange_out_rim, width=0.01)
    drum_wood_face = create_child_cylinder("Winch_Wood_Face", radius=0.126, depth=0.024, vertices=12, parent=base_yaw, local_loc=(-0.31, winch_y, winch_z), local_rot=(0, math.pi/2, 0), mat=mats['oak'])
    create_child_bolt("Winch_Central_Nut", radius=0.032, depth=0.028, parent=base_yaw, local_loc=(-0.328, winch_y, winch_z), local_rot=(0, math.pi/2, 0), mat=mats['bands'])

    # Coiled Hemp Rope on Drum: 6 thick neatly wound coils
    drum_rope_core = create_child_cylinder("Winch_Rope_Core", radius=0.112, depth=0.12, vertices=12, parent=base_yaw, local_loc=(-0.245, winch_y, winch_z), local_rot=(0, math.pi/2, 0), mat=mats['rope'])
    for rx in [-0.20, -0.218, -0.236, -0.254, -0.272, -0.29]:
        create_child_cylinder(f"Winch_Rope_Turn_{rx}", radius=0.126, depth=0.018, vertices=12, parent=base_yaw, local_loc=(rx, winch_y, winch_z), local_rot=(0, math.pi/2, 0), mat=mats['rope'])

    # Ratchet Mechanism: Upper Gear, Dog Linkage & Brake Lever on Left Cheek (matching reference!)
    # Upper gear near trunnion
    ratchet_gear_upper = create_child_cylinder("Ratchet_Gear_Upper", radius=0.048, depth=0.020, vertices=12, parent=base_yaw, local_loc=(-0.165, 0.03, 0.33), local_rot=(0, math.pi/2, 0), mat=mats['bands'])
    for i in range(8):
        ang = i * (math.pi / 4.0)
        create_child_cube(f"GearTooth_U_{i}", size=(0.016, 0.022, 0.012), parent=base_yaw, local_loc=(-0.165, 0.03 + 0.046*math.cos(ang), 0.33 + 0.046*math.sin(ang)), local_rot=(ang, 0, 0), mat=mats['bands'])

    # Lower small gear near drum
    ratchet_gear_lower = create_child_cylinder("Ratchet_Gear_Lower", radius=0.034, depth=0.020, vertices=10, parent=base_yaw, local_loc=(-0.165, -0.05, 0.25), local_rot=(0, math.pi/2, 0), mat=mats['bands'])
    for i in range(6):
        ang = i * (math.pi / 3.0)
        create_child_cube(f"GearTooth_L_{i}", size=(0.014, 0.018, 0.010), parent=base_yaw, local_loc=(-0.165, -0.05 + 0.033*math.cos(ang), 0.25 + 0.033*math.sin(ang)), local_rot=(ang, 0, 0), mat=mats['bands'])

    # Iron linkage bracket connecting trunnion hub to lower axle
    linkage_bar = create_child_cube("Ratchet_Linkage_Bar", size=(0.014, 0.024, 0.13), parent=base_yaw, local_loc=(-0.160, -0.01, 0.29), local_rot=(0.60, 0, 0), mat=mats['bands'])
    set_flat_and_bevel(linkage_bar, width=0.004)

    # Pawl / Brake lever arm extending up and backwards with turned wooden handle
    lever_arm = create_child_cube("Ratchet_Lever_Arm", size=(0.018, 0.022, 0.17), parent=base_yaw, local_loc=(-0.175, -0.02, 0.35), local_rot=(0.48, 0, 0.08), mat=mats['bands'])
    set_flat_and_bevel(lever_arm, width=0.005)
    lever_handle = create_child_cylinder("Ratchet_Lever_Handle", radius=0.018, depth=0.08, vertices=6, parent=base_yaw, local_loc=(-0.175, -0.08, 0.43), local_rot=(0.48, 0, 0), mat=mats['oak'])

    # =========================================================================
    # 3. BARREL (Barrel_Pitch - Pivots vertically at Z = 0.360 relative to Base_Yaw)
    # =========================================================================
    barrel_angle = math.radians(6.5) # Upward elevation pitch
    barrel = bpy.data.objects.new("Barrel_Pitch", None)
    barrel.empty_display_type = 'ARROWS'
    barrel.empty_display_size = 0.25
    bpy.context.collection.objects.link(barrel)
    barrel.parent = base_yaw
    barrel.matrix_parent_inverse.identity()
    barrel.location = (0, 0, 0.360)
    barrel.rotation_euler = (barrel_angle, 0, 0)

    # 3.1 Trunnions (Pivot Axle through bearing blocks)
    trunnions = create_child_cylinder("Barrel_Trunnions", radius=0.040, depth=0.38, vertices=8, parent=barrel, local_loc=(0, 0, 0), local_rot=(0, math.pi/2, 0), mat=mats['bands'])

    # 3.2 Octagonal Barrel Body (Forward along +Y, Up along +Z)
    # Breech body (rear)
    barrel_rear = create_child_cylinder("Barrel_Rear_Body", radius=0.150, depth=0.22, vertices=8, parent=barrel, local_loc=(0, -0.28, 0), local_rot=(math.pi/2, 0, 0), mat=mats['iron'])
    set_flat_and_bevel(barrel_rear, width=0.012)

    # Rear reinforce hoop
    hoop_rear = create_child_cylinder("Barrel_Hoop_Rear", radius=0.168, depth=0.060, vertices=8, parent=barrel, local_loc=(0, -0.17, 0), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])
    set_flat_and_bevel(hoop_rear, width=0.01)

    # Mid body
    barrel_mid = create_child_cylinder("Barrel_Mid_Body", radius=0.138, depth=0.35, vertices=8, parent=barrel, local_loc=(0, 0.045, 0), local_rot=(math.pi/2, 0, 0), mat=mats['iron'])
    set_flat_and_bevel(barrel_mid, width=0.01)

    # Mid reinforce hoop
    hoop_mid = create_child_cylinder("Barrel_Hoop_Mid", radius=0.156, depth=0.060, vertices=8, parent=barrel, local_loc=(0, 0.23, 0), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])
    set_flat_and_bevel(hoop_mid, width=0.01)

    # Front body (Chase)
    barrel_front = create_child_cylinder("Barrel_Front_Body", radius=0.126, depth=0.28, vertices=8, parent=barrel, local_loc=(0, 0.40, 0), local_rot=(math.pi/2, 0, 0), mat=mats['iron'])
    set_flat_and_bevel(barrel_front, width=0.008)

    # Muzzle Ring / Swell (Massive flared octagonal muzzle band)
    muzzle_ring = create_child_cylinder("Barrel_Muzzle_Ring", radius=0.165, depth=0.090, vertices=8, parent=barrel, local_loc=(0, 0.58, 0), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])
    set_flat_and_bevel(muzzle_ring, width=0.014)

    # Bore hole
    bore = create_child_cylinder("Barrel_Bore_Hole", radius=0.088, depth=0.45, vertices=8, parent=barrel, local_loc=(0, 0.47, 0), local_rot=(math.pi/2, 0, 0), mat=mats['bore'])

    # Breech rear cap
    cascabel = create_child_cylinder("Barrel_Breech_Cap", radius=0.134, depth=0.048, vertices=8, parent=barrel, local_loc=(0, -0.40, 0), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])
    set_flat_and_bevel(cascabel, width=0.01)

    # Breech Crank Handle (Mounted on right side at rear, matching reference!)
    crank_hub = create_child_cylinder("Crank_Hub", radius=0.040, depth=0.040, vertices=8, parent=barrel, local_loc=(0, -0.435, 0), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])
    # Crank arm extends to the right (+X) and backward (-Y)
    crank_arm = create_child_cube("Crank_Arm", size=(0.14, 0.024, 0.024), parent=barrel, local_loc=(0.09, -0.445, -0.01), local_rot=(0, 0, -0.2), mat=mats['bands'])
    set_flat_and_bevel(crank_arm, width=0.005)
    crank_pin = create_child_cylinder("Crank_Pin", radius=0.014, depth=0.07, vertices=6, parent=barrel, local_loc=(0.16, -0.48, -0.01), local_rot=(math.pi/2, 0, 0), mat=mats['bands'])
    crank_handle = create_child_cylinder("Crank_Handle", radius=0.022, depth=0.11, vertices=6, parent=barrel, local_loc=(0.16, -0.55, -0.01), local_rot=(math.pi/2, 0, 0), mat=mats['oak'])

    # =========================================================================
    # 4. HARPOON PROJECTILE (Harpoon_Projectile - loaded in barrel)
    # =========================================================================
    # Hexagonal / octagonal steel shaft
    harpoon = create_child_cylinder("Harpoon_Projectile", radius=0.035, depth=0.96, vertices=8, parent=barrel, local_loc=(0, 0.72, 0), local_rot=(math.pi/2, 0, 0), mat=mats['steel'])
    set_flat_and_bevel(harpoon, width=0.004)

    # 4.1 Faceted Barbed Arrowhead Tip with sharp hooked barbs (matching reference!)
    tip_mesh = bpy.data.meshes.new("Harpoon_Tip_Mesh")
    tip_obj = bpy.data.objects.new("Harpoon_Tip", tip_mesh)
    bpy.context.collection.objects.link(tip_obj)
    tip_obj.parent = barrel
    tip_obj.matrix_parent_inverse.identity()
    # Rotated so the barbs flare vertically/diagonally into the camera view!
    tip_obj.location = (0, 0, 0)
    tip_obj.rotation_euler = (0, math.radians(45), 0)
    
    bm = bmesh.new()
    # Sharp forward point
    v_tip = bm.verts.new((0, 1.40, 0))
    # Mid blade ridges
    v_t_top = bm.verts.new((0, 1.20, 0.045))
    v_t_bot = bm.verts.new((0, 1.20, -0.045))
    # Outer barb tips (sharp flared wings)
    v_barb_l = bm.verts.new((-0.20, 0.98, 0))
    v_barb_r = bm.verts.new((0.20, 0.98, 0))
    # Inner hooked notches
    v_notch_l = bm.verts.new((-0.07, 1.07, 0))
    v_notch_r = bm.verts.new((0.07, 1.07, 0))
    # Base collar vertices
    v_base_top = bm.verts.new((0, 0.94, 0.035))
    v_base_bot = bm.verts.new((0, 0.94, -0.035))
    v_base_c = bm.verts.new((0, 0.92, 0))

    # Top faces
    bm.faces.new((v_tip, v_barb_l, v_t_top))
    bm.faces.new((v_tip, v_t_top, v_barb_r))
    bm.faces.new((v_barb_l, v_notch_l, v_t_top))
    bm.faces.new((v_barb_r, v_t_top, v_notch_r))
    bm.faces.new((v_notch_l, v_base_top, v_t_top))
    bm.faces.new((v_notch_r, v_t_top, v_base_top))

    # Bottom faces
    bm.faces.new((v_tip, v_t_bot, v_barb_l))
    bm.faces.new((v_tip, v_barb_r, v_t_bot))
    bm.faces.new((v_barb_l, v_t_bot, v_notch_l))
    bm.faces.new((v_barb_r, v_notch_r, v_t_bot))
    bm.faces.new((v_notch_l, v_t_bot, v_base_bot))
    bm.faces.new((v_notch_r, v_base_bot, v_t_bot))

    # Base closure
    bm.faces.new((v_notch_l, v_base_c, v_base_top))
    bm.faces.new((v_notch_l, v_base_bot, v_base_c))
    bm.faces.new((v_notch_r, v_base_top, v_base_c))
    bm.faces.new((v_notch_r, v_base_c, v_base_bot))

    bm.normal_update()
    bm.to_mesh(tip_mesh)
    bm.free()
    assign_mat(tip_obj, mats['steel'])
    tip_obj.data.shade_flat()
    set_flat_and_bevel(tip_obj, width=0.005)

    # 4.2 Wrapped Rope Collar / Knot around Harpoon Shaft
    rope_knot = create_child_cylinder("Harpoon_Rope_Knot", radius=0.060, depth=0.065, vertices=10, parent=barrel, local_loc=(0, 0.96, 0), local_rot=(math.pi/2, 0, 0), mat=mats['rope'])
    set_flat_and_bevel(rope_knot, width=0.008)

    # =========================================================================
    # 5. ROPE (Rope_Dynamic - Hanging curve entering underside of drum)
    # =========================================================================
    curve_data = bpy.data.curves.new('Rope_Path', type='CURVE')
    curve_data.dimensions = '3D'
    curve_data.resolution_u = 36
    curve_data.bevel_depth = 0.022 # Stout nautical rope
    curve_data.bevel_resolution = 2
    curve_data.use_fill_caps = True
    
    rope_obj = bpy.data.objects.new('Rope_Dynamic', curve_data)
    bpy.context.collection.objects.link(rope_obj)
    assign_mat(rope_obj, mats['rope'])
    rope_obj.parent = base_yaw
    rope_obj.matrix_parent_inverse.identity()

    bpy.context.view_layer.update()

    # Exact connection points calculated in local space of Base_Yaw
    inv = rope_obj.matrix_world.inverted()
    p0_local = inv @ (rope_knot.matrix_world.translation + Vector((-0.03, 0, -0.04)))
    # Enters neatly at the underside of the drum coils
    winch_entry_world = drum_rope_core.matrix_world.translation + Vector((-0.02, 0.03, -0.09))
    p3_local = inv @ winch_entry_world

    # 4-point smooth bezier catenary curve gracefully drooping above rail beam
    p1_local = p0_local + Vector((-0.10, -0.24, -0.30))
    p2_local = p3_local + Vector((-0.14, 0.28, -0.06))

    spline = curve_data.splines.new('BEZIER')
    spline.bezier_points.add(3)
    spline.bezier_points[0].co = p0_local
    spline.bezier_points[1].co = p1_local
    spline.bezier_points[2].co = p2_local
    spline.bezier_points[3].co = p3_local

    for bp in spline.bezier_points:
        bp.handle_left_type = 'AUTO'
        bp.handle_right_type = 'AUTO'

    bpy.context.view_layer.update()

    print("Harpoon Gun v12 built successfully.")

if __name__ == "__main__":
    build_harpoon_gun()
