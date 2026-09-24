import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix, Euler

def build_megalodon():
    print("--- Starting Polished Shark Build (Seamless Countershading) ---")

    # 1. Purge all existing objects from current scene
    for obj in list(bpy.context.scene.objects):
        bpy.data.objects.remove(obj, do_unlink=True)

    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)
    for a in list(bpy.data.armatures):
        if a.users == 0:
            bpy.data.armatures.remove(a)
    for act in list(bpy.data.actions):
        if act.users == 0:
            bpy.data.actions.remove(act)

    col_name = "Shark"
    if col_name in bpy.data.collections:
        col = bpy.data.collections[col_name]
    else:
        col = bpy.data.collections.new(col_name)
        bpy.context.scene.collection.children.link(col)

    # 2. Materials Setup
    def get_or_create_mat(name, color, roughness=0.5, metallic=0.0):
        if name in bpy.data.materials:
            mat = bpy.data.materials[name]
        else:
            mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        mat.diffuse_color = color
        nodes = mat.node_tree.nodes
        bsdf = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
        if bsdf:
            if "Base Color" in bsdf.inputs:
                bsdf.inputs["Base Color"].default_value = color
            if "Roughness" in bsdf.inputs:
                bsdf.inputs["Roughness"].default_value = roughness
            if "Metallic" in bsdf.inputs:
                bsdf.inputs["Metallic"].default_value = metallic
        return mat

    mat_dark  = get_or_create_mat("Shark_Skin_Dark", (0.12, 0.16, 0.22, 1.0), roughness=0.6)
    mat_belly = get_or_create_mat("Shark_Skin_Belly", (0.84, 0.82, 0.76, 1.0), roughness=0.65)
    mat_teeth = get_or_create_mat("Shark_Teeth", (0.95, 0.93, 0.88, 1.0), roughness=0.25)
    mat_mouth = get_or_create_mat("Shark_Mouth_Inside", (0.35, 0.07, 0.09, 1.0), roughness=0.45)
    mat_eyes  = get_or_create_mat("Shark_Eyes", (0.02, 0.02, 0.03, 1.0), roughness=0.05, metallic=0.3)

    materials_list = [mat_dark, mat_belly, mat_teeth, mat_mouth, mat_eyes]

    def assign_mats_to_mesh(mesh):
        mesh.materials.clear()
        for m in materials_list:
            mesh.materials.append(m)

    # 3. Create Armature
    arm_data = bpy.data.armatures.new("Shark_Armature")
    arm_obj = bpy.data.objects.new("Shark_Armature", arm_data)
    col.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    arm_obj.select_set(True)

    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones

    b_root = eb.new("Root")
    b_root.head = Vector((0.0, 0.0, 0.0))
    b_root.tail = Vector((0.0, 0.12, 0.0))

    b_sp1 = eb.new("Spine_01")
    b_sp1.head = Vector((0.0, 0.15, 0.05))
    b_sp1.tail = Vector((0.0, -0.42, 0.05))
    b_sp1.parent = b_root

    b_head = eb.new("Head")
    b_head.head = Vector((0.0, -0.42, 0.05))
    b_head.tail = Vector((0.0, -1.45, 0.0))
    b_head.parent = b_sp1

    # Jaw hinge at (0.0, -0.68, -0.16)
    b_jaw = eb.new("Jaw_Lower")
    b_jaw.head = Vector((0.0, -0.68, -0.16))
    b_jaw.tail = Vector((0.0, -1.48, -0.22))
    b_jaw.parent = b_head

    b_pec_l = eb.new("Pectoral_L")
    b_pec_l.head = Vector((-0.55, -0.20, -0.18))
    b_pec_l.tail = Vector((-1.20, 0.20, -0.45))
    b_pec_l.parent = b_sp1

    b_pec_r = eb.new("Pectoral_R")
    b_pec_r.head = Vector((0.55, -0.20, -0.18))
    b_pec_r.tail = Vector((1.20, 0.20, -0.45))
    b_pec_r.parent = b_sp1

    b_dor = eb.new("Dorsal_Fin")
    b_dor.head = Vector((0.0, 0.05, 0.58))
    b_dor.tail = Vector((0.0, 0.12, 1.35))
    b_dor.parent = b_sp1

    b_sp2 = eb.new("Spine_02")
    b_sp2.head = Vector((0.0, 0.15, 0.05))
    b_sp2.tail = Vector((0.0, 0.82, 0.05))
    b_sp2.parent = b_root

    b_t1 = eb.new("Tail_01")
    b_t1.head = Vector((0.0, 0.82, 0.05))
    b_t1.tail = Vector((0.0, 1.35, 0.05))
    b_t1.parent = b_sp2

    b_t2 = eb.new("Tail_02")
    b_t2.head = Vector((0.0, 1.35, 0.05))
    b_t2.tail = Vector((0.0, 1.82, 0.02))
    b_t2.parent = b_t1

    b_tfin = eb.new("Tail_Fin")
    b_tfin.head = Vector((0.0, 1.82, 0.02))
    b_tfin.tail = Vector((0.0, 2.40, 0.30))
    b_tfin.parent = b_t2

    bpy.ops.object.mode_set(mode='OBJECT')

    # 4. Create Shark_Body mesh
    mesh_body = bpy.data.meshes.new("Shark_Body")
    assign_mats_to_mesh(mesh_body)

    bm_body = bmesh.new()

    ring_defs = [
        {"y": -1.85, "wx": 0.24, "z_top": 0.12, "z_bot": -0.05, "is_rostrum": True},
        {"y": -1.52, "wx": 0.46, "z_top": 0.24, "z_bot": -0.10, "is_snout": True},
        {"y": -1.18, "wx": 0.58, "z_top": 0.40, "z_bot": -0.18, "is_head": True},
        {"y": -0.74, "wx": 0.60, "z_top": 0.56, "z_bot": -0.38, "is_gills": True},
        {"y": -0.18, "wx": 0.60, "z_top": 0.65, "z_bot": -0.52},
        {"y": +0.38, "wx": 0.50, "z_top": 0.58, "z_bot": -0.48},
        {"y": +0.95, "wx": 0.36, "z_top": 0.45, "z_bot": -0.38},
        {"y": +1.44, "wx": 0.24, "z_top": 0.32, "z_bot": -0.26},
        {"y": +1.78, "wx": 0.18, "z_top": 0.20, "z_bot": -0.16, "is_keel": True},
        {"y": +2.05, "wx": 0.10, "z_top": 0.15, "z_bot": -0.12}
    ]

    body_rings = []
    angles = [
        0.0, math.radians(30), math.radians(60), math.radians(90),
        math.radians(120), math.radians(150), math.radians(180),
        math.radians(210), math.radians(240), math.radians(270),
        math.radians(300), math.radians(330)
    ]

    for r_idx, r in enumerate(ring_defs):
        y = r["y"]
        wx = r["wx"]
        zt = r["z_top"]
        zb = r["z_bot"]
        zc = (zt + zb) * 0.5
        zh = (zt - zb) * 0.5

        ring_verts = []
        for i, ang in enumerate(angles):
            sx = math.sin(ang)
            cz = math.cos(ang)

            vx = sx * wx
            vz = zc + cz * zh

            if r.get("is_rostrum"):
                if i == 6:
                    vz = zb + 0.02
            elif r.get("is_snout") or r.get("is_head"):
                if i in (2, 10):
                    vx *= 1.08
                    vz += 0.04
                if r_idx in (1, 2):
                    if i == 6:
                        vz = zc - 0.04
                    elif i in (5, 7):
                        vz = zc - 0.07
                    elif i in (4, 8):
                        vz = zc - 0.09
            elif r.get("is_keel"):
                if i in (3, 9):
                    vx *= 1.35
            
            v = bm_body.verts.new((vx, y, vz))
            ring_verts.append(v)
        
        body_rings.append(ring_verts)

    # Cap front snout
    v_tip = bm_body.verts.new((0.0, -1.90, (ring_defs[0]["z_top"] + ring_defs[0]["z_bot"]) * 0.5))
    r0 = body_rings[0]
    for i in range(12):
        i_next = (i + 1) % 12
        f = bm_body.faces.new([v_tip, r0[i], r0[i_next]])
        f.material_index = 0 if (i in (0, 1, 2, 9, 10, 11)) else 1

    # Connect adjacent rings
    for r_idx in range(len(body_rings) - 1):
        r_curr = body_rings[r_idx]
        r_next = body_rings[r_idx + 1]

        for i in range(12):
            i_next = (i + 1) % 12
            v1 = r_curr[i]
            v2 = r_next[i]
            v3 = r_next[i_next]
            v4 = r_curr[i_next]

            f = bm_body.faces.new([v1, v2, v3, v4])
            # Seamless countershading:
            # 0, 1, 2, 10, 11: dorsal dark
            # 3, 9: lateral flank dark
            # 4, 8: lateral lower belly
            # 5, 6, 7: ventral midline
            if i in (0, 1, 2, 10, 11):
                f.material_index = 0 # dark
            elif i in (3, 9):
                f.material_index = 0 if r_idx >= 1 else 1
            elif i in (4, 5, 6, 7, 8):
                if r_idx in (1, 2) and i in (5, 6, 7):
                    f.material_index = 3 # mouth palate inside
                else:
                    f.material_index = 1 # belly
            else:
                f.material_index = 0

    # 5. Add Dorsal Fin
    v_d_bf_l = bm_body.verts.new((-0.05, -0.20, 0.65))
    v_d_bf_r = bm_body.verts.new((+0.05, -0.20, 0.65))
    v_d_br_l = bm_body.verts.new((-0.03, +0.45, 0.58))
    v_d_br_r = bm_body.verts.new((+0.03, +0.45, 0.58))
    v_d_tip  = bm_body.verts.new((0.0, +0.10, 1.40))
    v_d_notch= bm_body.verts.new((0.0, +0.34, 0.88))
    v_d_rtip = bm_body.verts.new((0.0, +0.44, 0.70))

    f_dr1 = bm_body.faces.new([v_d_bf_r, v_d_tip, v_d_br_r])
    f_dr2 = bm_body.faces.new([v_d_tip, v_d_notch, v_d_br_r])
    f_dr3 = bm_body.faces.new([v_d_notch, v_d_rtip, v_d_br_r])
    f_dl1 = bm_body.faces.new([v_d_bf_l, v_d_br_l, v_d_tip])
    f_dl2 = bm_body.faces.new([v_d_tip, v_d_br_l, v_d_notch])
    f_dl3 = bm_body.faces.new([v_d_notch, v_d_br_l, v_d_rtip])
    f_df = bm_body.faces.new([v_d_bf_l, v_d_tip, v_d_bf_r])
    f_dn = bm_body.faces.new([v_d_rtip, v_d_br_l, v_d_br_r])
    for f in (f_dr1, f_dr2, f_dr3, f_dl1, f_dl2, f_dl3, f_df, f_dn):
        f.material_index = 0

    # 6. Add 2nd Dorsal Fin
    v_d2_bf_l = bm_body.verts.new((-0.02, +1.40, 0.32))
    v_d2_bf_r = bm_body.verts.new((+0.02, +1.40, 0.32))
    v_d2_br_l = bm_body.verts.new((-0.015, +1.62, 0.25))
    v_d2_br_r = bm_body.verts.new((+0.015, +1.62, 0.25))
    v_d2_tip  = bm_body.verts.new((0.0, +1.58, 0.50))
    f_d2_r = bm_body.faces.new([v_d2_bf_r, v_d2_tip, v_d2_br_r])
    f_d2_l = bm_body.faces.new([v_d2_bf_l, v_d2_br_l, v_d2_tip])
    f_d2_f = bm_body.faces.new([v_d2_bf_l, v_d2_tip, v_d2_bf_r])
    f_d2_b = bm_body.faces.new([v_d2_br_l, v_d2_br_r, v_d2_tip])
    for f in (f_d2_r, f_d2_l, f_d2_f, f_d2_b):
        f.material_index = 0

    # 7. Add Anal Fin
    v_an_bf_l = bm_body.verts.new((-0.02, +1.44, -0.26))
    v_an_bf_r = bm_body.verts.new((+0.02, +1.44, -0.26))
    v_an_br_l = bm_body.verts.new((-0.015, +1.65, -0.20))
    v_an_br_r = bm_body.verts.new((+0.015, +1.65, -0.20))
    v_an_tip  = bm_body.verts.new((0.0, +1.62, -0.46))
    f_an_r = bm_body.faces.new([v_an_bf_r, v_an_br_r, v_an_tip])
    f_an_l = bm_body.faces.new([v_an_bf_l, v_an_tip, v_an_br_l])
    f_an_f = bm_body.faces.new([v_an_bf_l, v_an_bf_r, v_an_tip])
    f_an_b = bm_body.faces.new([v_an_br_l, v_an_tip, v_an_br_r])
    for f in (f_an_r, f_an_l, f_an_f, f_an_b):
        f.material_index = 1

    # 8. Add Heterocercal Caudal Fin
    v_t_root_top = bm_body.verts.new((0.0, +2.05, +0.15))
    v_t_root_bot = bm_body.verts.new((0.0, +2.05, -0.12))
    v_t_up_tip   = bm_body.verts.new((0.0, +2.76, +1.20))
    v_t_notch_out= bm_body.verts.new((0.0, +2.71, +0.90))
    v_t_notch_in = bm_body.verts.new((0.0, +2.64, +0.92))
    v_t_fork     = bm_body.verts.new((0.0, +2.35, +0.22))
    v_t_low_tip  = bm_body.verts.new((0.0, +2.48, -0.46))

    v_t_mid_r    = bm_body.verts.new((+0.035, +2.30, +0.22))
    v_t_mid_l    = bm_body.verts.new((-0.035, +2.30, +0.22))
    v_t_up_mid_r = bm_body.verts.new((+0.025, +2.40, +0.68))
    v_t_up_mid_l = bm_body.verts.new((-0.025, +2.40, +0.68))

    f_tr1 = bm_body.faces.new([v_t_root_top, v_t_up_mid_r, v_t_mid_r, v_t_root_bot])
    f_tr2 = bm_body.faces.new([v_t_root_top, v_t_up_tip, v_t_up_mid_r])
    f_tr3 = bm_body.faces.new([v_t_up_tip, v_t_notch_out, v_t_notch_in, v_t_up_mid_r])
    f_tr4 = bm_body.faces.new([v_t_up_mid_r, v_t_notch_in, v_t_fork, v_t_mid_r])
    f_tr5 = bm_body.faces.new([v_t_mid_r, v_t_fork, v_t_low_tip, v_t_root_bot])
    for f in (f_tr1, f_tr2, f_tr3, f_tr4, f_tr5):
        f.material_index = 0

    f_tl1 = bm_body.faces.new([v_t_root_top, v_t_root_bot, v_t_mid_l, v_t_up_mid_l])
    f_tl2 = bm_body.faces.new([v_t_root_top, v_t_up_mid_l, v_t_up_tip])
    f_tl3 = bm_body.faces.new([v_t_up_tip, v_t_up_mid_l, v_t_notch_in, v_t_notch_out])
    f_tl4 = bm_body.faces.new([v_t_up_mid_l, v_t_mid_l, v_t_fork, v_t_notch_in])
    f_tl5 = bm_body.faces.new([v_t_mid_l, v_t_root_bot, v_t_low_tip, v_t_fork])
    for f in (f_tl1, f_tl2, f_tl3, f_tl4, f_tl5):
        f.material_index = 0

    # 9. Add Pectoral Fins
    for side in (-1.0, 1.0):
        v_p_b_front = bm_body.verts.new((side * 0.58, -0.30, -0.22))
        v_p_b_rear  = bm_body.verts.new((side * 0.54, +0.06, -0.25))
        v_p_tip     = bm_body.verts.new((side * 1.30, +0.28, -0.50))
        v_p_rear_cut= bm_body.verts.new((side * 0.85, +0.24, -0.34))
        v_p_mid_top = bm_body.verts.new((side * 0.88, -0.04, -0.32))
        v_p_mid_bot = bm_body.verts.new((side * 0.88, -0.04, -0.38))

        if side > 0:
            f_p_t1 = bm_body.faces.new([v_p_b_front, v_p_mid_top, v_p_b_rear])
            f_p_t2 = bm_body.faces.new([v_p_b_front, v_p_tip, v_p_mid_top])
            f_p_t3 = bm_body.faces.new([v_p_mid_top, v_p_tip, v_p_rear_cut, v_p_b_rear])
            f_p_b1 = bm_body.faces.new([v_p_b_front, v_p_b_rear, v_p_mid_bot])
            f_p_b2 = bm_body.faces.new([v_p_b_front, v_p_mid_bot, v_p_tip])
            f_p_b3 = bm_body.faces.new([v_p_mid_bot, v_p_b_rear, v_p_rear_cut, v_p_tip])
        else:
            f_p_t1 = bm_body.faces.new([v_p_b_front, v_p_b_rear, v_p_mid_top])
            f_p_t2 = bm_body.faces.new([v_p_b_front, v_p_mid_top, v_p_tip])
            f_p_t3 = bm_body.faces.new([v_p_mid_top, v_p_b_rear, v_p_rear_cut, v_p_tip])
            f_p_b1 = bm_body.faces.new([v_p_b_front, v_p_mid_bot, v_p_b_rear])
            f_p_b2 = bm_body.faces.new([v_p_b_front, v_p_tip, v_p_mid_bot])
            f_p_b3 = bm_body.faces.new([v_p_mid_bot, v_p_tip, v_p_rear_cut, v_p_b_rear])

        for f in (f_p_t1, f_p_t2, f_p_t3):
            f.material_index = 0
        for f in (f_p_b1, f_p_b2, f_p_b3):
            f.material_index = 1

    # 10. Add Pelvic Fins
    for side in (-1.0, 1.0):
        v_pl_b1 = bm_body.verts.new((side * 0.28, +0.90, -0.38))
        v_pl_b2 = bm_body.verts.new((side * 0.22, +1.12, -0.34))
        v_pl_tip = bm_body.verts.new((side * 0.42, +1.20, -0.50))
        v_pl_mid_t = bm_body.verts.new((side * 0.33, +1.04, -0.38))
        v_pl_mid_b = bm_body.verts.new((side * 0.33, +1.04, -0.44))

        if side > 0:
            f_pl1 = bm_body.faces.new([v_pl_b1, v_pl_mid_t, v_pl_b2])
            f_pl2 = bm_body.faces.new([v_pl_b1, v_pl_tip, v_pl_mid_t])
            f_pl3 = bm_body.faces.new([v_pl_mid_t, v_pl_tip, v_pl_b2])
            f_pl4 = bm_body.faces.new([v_pl_b1, v_pl_b2, v_pl_mid_b])
            f_pl5 = bm_body.faces.new([v_pl_b1, v_pl_mid_b, v_pl_tip])
            f_pl6 = bm_body.faces.new([v_pl_mid_b, v_pl_b2, v_pl_tip])
        else:
            f_pl1 = bm_body.faces.new([v_pl_b1, v_pl_b2, v_pl_mid_t])
            f_pl2 = bm_body.faces.new([v_pl_b1, v_pl_mid_t, v_pl_tip])
            f_pl3 = bm_body.faces.new([v_pl_mid_t, v_pl_b2, v_pl_tip])
            f_pl4 = bm_body.faces.new([v_pl_b1, v_pl_mid_b, v_pl_b2])
            f_pl5 = bm_body.faces.new([v_pl_b1, v_pl_tip, v_pl_mid_b])
            f_pl6 = bm_body.faces.new([v_pl_mid_b, v_pl_tip, v_pl_b2])
        for f in (f_pl1, f_pl2, f_pl3, f_pl4, f_pl5, f_pl6):
            f.material_index = 1

    # 11. Add Faceted Diamond Eyes
    for side in (-1.0, 1.0):
        eye_center = Vector((side * 0.52, -1.22, 0.18))
        r_eye = 0.07
        v_e_f = bm_body.verts.new(eye_center + Vector((0.0, -r_eye, 0.0)))
        v_e_b = bm_body.verts.new(eye_center + Vector((0.0, +r_eye, 0.0)))
        v_e_u = bm_body.verts.new(eye_center + Vector((0.0, 0.0, +r_eye * 0.9)))
        v_e_d = bm_body.verts.new(eye_center + Vector((0.0, 0.0, -r_eye * 0.9)))
        v_e_o = bm_body.verts.new(eye_center + Vector((side * r_eye * 1.1, 0.0, 0.0)))

        if side > 0:
            fe1 = bm_body.faces.new([v_e_f, v_e_o, v_e_u])
            fe2 = bm_body.faces.new([v_e_u, v_e_o, v_e_b])
            fe3 = bm_body.faces.new([v_e_b, v_e_o, v_e_d])
            fe4 = bm_body.faces.new([v_e_d, v_e_o, v_e_f])
        else:
            fe1 = bm_body.faces.new([v_e_f, v_e_u, v_e_o])
            fe2 = bm_body.faces.new([v_e_u, v_e_b, v_e_o])
            fe3 = bm_body.faces.new([v_e_b, v_e_d, v_e_o])
            fe4 = bm_body.faces.new([v_e_d, v_e_f, v_e_o])
        for fe in (fe1, fe2, fe3, fe4):
            fe.material_index = 4 # Shark_Eyes

    # 12. Add 5 Stylized Gill Slits on Each Flank
    for side in (-1.0, 1.0):
        for g in range(5):
            gy = -0.98 + g * 0.095
            gz = 0.02 - g * 0.015
            gx = side * 0.59
            vg1 = bm_body.verts.new((gx + side * 0.015, gy - 0.012, gz + 0.14))
            vg2 = bm_body.verts.new((gx + side * 0.025, gy + 0.010, gz + 0.12))
            vg3 = bm_body.verts.new((gx + side * 0.025, gy + 0.010, gz - 0.12))
            vg4 = bm_body.verts.new((gx + side * 0.015, gy - 0.012, gz - 0.14))
            if side > 0:
                fg = bm_body.faces.new([vg1, vg2, vg3, vg4])
            else:
                fg = bm_body.faces.new([vg1, vg4, vg3, vg2])
            fg.material_index = 0

    # 13. Add Upper Teeth along Upper Jaw Rim
    num_teeth_upper = 14
    for t_i in range(num_teeth_upper):
        frac = t_i / (num_teeth_upper - 1)
        ang = math.pi * (1.0 - frac)
        tx = 0.44 * math.cos(ang)
        ty = -1.50 + 0.72 * (abs(tx) / 0.44)**1.6
        tz = -0.11 - 0.06 * (abs(tx) / 0.44)

        t_len = 0.10 - 0.03 * (abs(tx) / 0.44)
        t_w   = 0.035

        vt_b1 = bm_body.verts.new((tx - t_w * 0.7, ty - t_w * 0.3, tz))
        vt_b2 = bm_body.verts.new((tx + t_w * 0.7, ty - t_w * 0.3, tz))
        vt_b3 = bm_body.verts.new((tx, ty + t_w * 0.6, tz + 0.01))
        vt_tip = bm_body.verts.new((tx * 0.95, ty + 0.02, tz - t_len))

        ft1 = bm_body.faces.new([vt_b1, vt_b2, vt_tip])
        ft2 = bm_body.faces.new([vt_b2, vt_tip, vt_b3])
        ft3 = bm_body.faces.new([vt_b3, vt_tip, vt_b1])
        for ft in (ft1, ft2, ft3):
            ft.material_index = 2

    bm_body.to_mesh(mesh_body)
    bm_body.free()

    obj_body = bpy.data.objects.new("Shark_Body", mesh_body)
    col.objects.link(obj_body)

    # 14. Create Lower Jaw Mesh (`Shark_Jaw_Lower`)
    mesh_jaw = bpy.data.meshes.new("Shark_Jaw_Lower")
    assign_mats_to_mesh(mesh_jaw)

    bm_jaw = bmesh.new()

    jaw_pts = [
        {"x": -0.46, "y": -0.68, "z": -0.16, "w": 0.08},
        {"x": -0.42, "y": -1.02, "z": -0.18, "w": 0.10},
        {"x": -0.32, "y": -1.30, "z": -0.20, "w": 0.12},
        {"x":  0.00, "y": -1.48, "z": -0.23, "w": 0.15},
        {"x": +0.32, "y": -1.30, "z": -0.20, "w": 0.12},
        {"x": +0.42, "y": -1.02, "z": -0.18, "w": 0.10},
        {"x": +0.46, "y": -0.68, "z": -0.16, "w": 0.08},
    ]

    jaw_top_outer = []
    jaw_top_inner = []
    jaw_bot_outer = []
    jaw_bot_inner = []

    for pt in jaw_pts:
        px = pt["x"]
        py = pt["y"]
        pz = pt["z"]
        pw = pt["w"]
        jaw_top_outer.append(bm_jaw.verts.new((px, py, pz)))
        in_x = -0.06 if px > 0 else (0.06 if px < 0 else 0.0)
        jaw_top_inner.append(bm_jaw.verts.new((px + in_x, py + 0.04, pz + 0.01)))
        jaw_bot_outer.append(bm_jaw.verts.new((px * 0.92, py * 0.98, pz - pw)))
        jaw_bot_inner.append(bm_jaw.verts.new((px * 0.70, py + 0.07, pz - pw * 0.7)))

    num_j = len(jaw_pts)
    for j in range(num_j - 1):
        f_out = bm_jaw.faces.new([jaw_top_outer[j], jaw_top_outer[j+1], jaw_bot_outer[j+1], jaw_bot_outer[j]])
        f_out.material_index = 1

        f_rim = bm_jaw.faces.new([jaw_top_outer[j], jaw_top_inner[j], jaw_top_inner[j+1], jaw_top_outer[j+1]])
        f_rim.material_index = 1

        f_inn = bm_jaw.faces.new([jaw_top_inner[j], jaw_bot_inner[j], jaw_bot_inner[j+1], jaw_top_inner[j+1]])
        f_inn.material_index = 3

        f_bot = bm_jaw.faces.new([jaw_bot_outer[j], jaw_bot_outer[j+1], jaw_bot_inner[j+1], jaw_bot_inner[j]])
        f_bot.material_index = 1

    # Tongue / floor plate
    v_tongue_center = bm_jaw.verts.new((0.0, -0.98, -0.22))
    for j in range(num_j - 1):
        f_tg = bm_jaw.faces.new([jaw_bot_inner[j], jaw_bot_inner[j+1], v_tongue_center])
        f_tg.material_index = 3

    # Lower Teeth
    num_teeth_lower = 14
    for t_i in range(num_teeth_lower):
        frac = t_i / (num_teeth_lower - 1)
        ang = math.pi * (1.0 - frac)
        tx = 0.40 * math.cos(ang)
        ty = -1.42 + 0.66 * (abs(tx) / 0.40)**1.6
        tz = -0.20 - 0.04 * (abs(tx) / 0.40)

        t_len = 0.09 - 0.02 * (abs(tx) / 0.40)
        t_w   = 0.032

        vt_b1 = bm_jaw.verts.new((tx - t_w * 0.7, ty - t_w * 0.3, tz))
        vt_b2 = bm_jaw.verts.new((tx + t_w * 0.7, ty - t_w * 0.3, tz))
        vt_b3 = bm_jaw.verts.new((tx, ty + t_w * 0.6, tz - 0.01))
        vt_tip = bm_jaw.verts.new((tx * 0.95, ty + 0.02, tz + t_len))

        ft1 = bm_jaw.faces.new([vt_b1, vt_tip, vt_b2])
        ft2 = bm_jaw.faces.new([vt_b2, vt_tip, vt_b3])
        ft3 = bm_jaw.faces.new([vt_b3, vt_tip, vt_b1])
        for ft in (ft1, ft2, ft3):
            ft.material_index = 2

    bm_jaw.to_mesh(mesh_jaw)
    bm_jaw.free()

    obj_jaw = bpy.data.objects.new("Shark_Jaw_Lower", mesh_jaw)
    col.objects.link(obj_jaw)

    # Set Pivot for Shark_Jaw_Lower at hinge point (0.0, -0.68, -0.16)
    bpy.context.scene.cursor.location = Vector((0.0, -0.68, -0.16))
    bpy.context.view_layer.objects.active = obj_jaw
    obj_jaw.select_set(True)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    # Flat Shading
    for obj in (obj_body, obj_jaw):
        for poly in obj.data.polygons:
            poly.use_smooth = False

    # 15. Vertex Groups & Skinning
    vg_jaw = obj_jaw.vertex_groups.new(name="Jaw_Lower")
    for v in obj_jaw.data.vertices:
        vg_jaw.add([v.index], 1.0, 'REPLACE')

    bone_names = [
        "Root", "Spine_01", "Head", "Spine_02", "Tail_01", "Tail_02", "Tail_Fin",
        "Pectoral_L", "Pectoral_R", "Dorsal_Fin"
    ]
    b_vgs = {name: obj_body.vertex_groups.new(name=name) for name in bone_names}

    for v in obj_body.data.vertices:
        co = v.co
        x, y, z = co.x, co.y, co.z

        # Dorsal fin
        if z > 0.65 and -0.25 <= y <= 0.50 and abs(x) < 0.20:
            factor = min(1.0, (z - 0.65) / 0.40)
            b_vgs["Dorsal_Fin"].add([v.index], factor, 'REPLACE')
            b_vgs["Spine_01"].add([v.index], 1.0 - factor, 'REPLACE')
            continue

        # Left Pectoral
        if x < -0.55 and -0.40 <= y <= 0.35:
            factor = min(1.0, (abs(x) - 0.55) / 0.40)
            b_vgs["Pectoral_L"].add([v.index], factor, 'REPLACE')
            b_vgs["Spine_01"].add([v.index], 1.0 - factor, 'REPLACE')
            continue

        # Right Pectoral
        if x > 0.55 and -0.40 <= y <= 0.35:
            factor = min(1.0, (x - 0.55) / 0.40)
            b_vgs["Pectoral_R"].add([v.index], factor, 'REPLACE')
            b_vgs["Spine_01"].add([v.index], 1.0 - factor, 'REPLACE')
            continue

        # Tail Fin
        if y >= 2.00:
            b_vgs["Tail_Fin"].add([v.index], 1.0, 'REPLACE')
            continue

        # Axial Spine
        if y < -0.60:
            b_vgs["Head"].add([v.index], 1.0, 'REPLACE')
        elif -0.60 <= y < -0.15:
            t = (y - (-0.60)) / 0.45
            b_vgs["Head"].add([v.index], 1.0 - t, 'REPLACE')
            b_vgs["Spine_01"].add([v.index], t, 'REPLACE')
        elif -0.15 <= y < 0.15:
            b_vgs["Spine_01"].add([v.index], 1.0, 'REPLACE')
        elif 0.15 <= y < 0.50:
            t = (y - 0.15) / 0.35
            b_vgs["Spine_01"].add([v.index], 1.0 - t, 'REPLACE')
            b_vgs["Spine_02"].add([v.index], t, 'REPLACE')
        elif 0.50 <= y < 0.85:
            b_vgs["Spine_02"].add([v.index], 1.0, 'REPLACE')
        elif 0.85 <= y < 1.20:
            t = (y - 0.85) / 0.35
            b_vgs["Spine_02"].add([v.index], 1.0 - t, 'REPLACE')
            b_vgs["Tail_01"].add([v.index], t, 'REPLACE')
        elif 1.20 <= y < 1.50:
            b_vgs["Tail_01"].add([v.index], 1.0, 'REPLACE')
        elif 1.50 <= y < 1.75:
            t = (y - 1.50) / 0.25
            b_vgs["Tail_01"].add([v.index], 1.0 - t, 'REPLACE')
            b_vgs["Tail_02"].add([v.index], t, 'REPLACE')
        elif 1.75 <= y < 2.00:
            t = (y - 1.75) / 0.25
            b_vgs["Tail_02"].add([v.index], 1.0 - t, 'REPLACE')
            b_vgs["Tail_Fin"].add([v.index], t, 'REPLACE')

    # Add Armature Modifiers
    for obj in (obj_body, obj_jaw):
        mod = obj.modifiers.new("Armature", 'ARMATURE')
        mod.object = arm_obj
        obj.parent = arm_obj

    # 16. Create Animations
    bpy.context.view_layer.objects.active = arm_obj
    arm_obj.animation_data_create()
    pose_bones = arm_obj.pose.bones

    # --- Shark_Swim Action ---
    act_swim = bpy.data.actions.new(name="Shark_Swim")
    arm_obj.animation_data.action = act_swim
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = 40

    for pb in pose_bones:
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = Euler((0, 0, 0), 'XYZ')

    for f in range(0, 41, 5):
        t = (f / 40.0) * 2.0 * math.pi
        ang_sp1 = math.radians(3.5) * math.sin(t)
        pose_bones["Spine_01"].rotation_euler.z = ang_sp1
        pose_bones["Spine_01"].keyframe_insert(data_path="rotation_euler", index=2, frame=f)

        pose_bones["Head"].rotation_euler.z = -ang_sp1 * 0.6
        pose_bones["Head"].keyframe_insert(data_path="rotation_euler", index=2, frame=f)

        ang_sp2 = math.radians(7.0) * math.sin(t - 0.7)
        pose_bones["Spine_02"].rotation_euler.z = ang_sp2
        pose_bones["Spine_02"].keyframe_insert(data_path="rotation_euler", index=2, frame=f)

        ang_t1 = math.radians(13.0) * math.sin(t - 1.5)
        pose_bones["Tail_01"].rotation_euler.z = ang_t1
        pose_bones["Tail_01"].keyframe_insert(data_path="rotation_euler", index=2, frame=f)

        ang_t2 = math.radians(18.0) * math.sin(t - 2.3)
        pose_bones["Tail_02"].rotation_euler.z = ang_t2
        pose_bones["Tail_02"].keyframe_insert(data_path="rotation_euler", index=2, frame=f)

        ang_tfin = math.radians(24.0) * math.sin(t - 3.1)
        pose_bones["Tail_Fin"].rotation_euler.z = ang_tfin
        pose_bones["Tail_Fin"].keyframe_insert(data_path="rotation_euler", index=2, frame=f)

        ang_pec = math.radians(4.0) * math.sin(t)
        pose_bones["Pectoral_L"].rotation_euler.y = ang_pec
        pose_bones["Pectoral_L"].keyframe_insert(data_path="rotation_euler", index=1, frame=f)
        pose_bones["Pectoral_R"].rotation_euler.y = -ang_pec
        pose_bones["Pectoral_R"].keyframe_insert(data_path="rotation_euler", index=1, frame=f)

    # --- Shark_Bite Action ---
    act_bite = bpy.data.actions.new(name="Shark_Bite")
    arm_obj.animation_data.action = act_bite

    for pb in pose_bones:
        pb.rotation_euler = Euler((0, 0, 0), 'XYZ')

    # Frame 0: Mouth closed
    pose_bones["Jaw_Lower"].rotation_euler.x = 0.0
    pose_bones["Jaw_Lower"].keyframe_insert(data_path="rotation_euler", index=0, frame=0)
    pose_bones["Head"].rotation_euler.x = 0.0
    pose_bones["Head"].keyframe_insert(data_path="rotation_euler", index=0, frame=0)

    # Frame 8: Open WIDE DOWNWARDS (+45 deg jaw drops, -8 deg head lifts up)
    pose_bones["Jaw_Lower"].rotation_euler.x = math.radians(45.0)
    pose_bones["Jaw_Lower"].keyframe_insert(data_path="rotation_euler", index=0, frame=8)
    pose_bones["Head"].rotation_euler.x = math.radians(-8.0)
    pose_bones["Head"].keyframe_insert(data_path="rotation_euler", index=0, frame=8)

    # Frame 13: Violent snap clamp shut
    pose_bones["Jaw_Lower"].rotation_euler.x = math.radians(-3.0)
    pose_bones["Jaw_Lower"].keyframe_insert(data_path="rotation_euler", index=0, frame=13)
    pose_bones["Head"].rotation_euler.x = math.radians(1.5)
    pose_bones["Head"].keyframe_insert(data_path="rotation_euler", index=0, frame=13)

    # Frame 18: Settle to rest
    pose_bones["Jaw_Lower"].rotation_euler.x = 0.0
    pose_bones["Jaw_Lower"].keyframe_insert(data_path="rotation_euler", index=0, frame=18)
    pose_bones["Head"].rotation_euler.x = 0.0
    pose_bones["Head"].keyframe_insert(data_path="rotation_euler", index=0, frame=18)

    # Reset pose to rest
    for pb in pose_bones:
        pb.rotation_euler = Euler((0, 0, 0), 'XYZ')
    arm_obj.animation_data.action = act_swim

    # 17. Dimensions Check
    dim_body = obj_body.dimensions
    dim_jaw  = obj_jaw.dimensions

    # 18. Save .blend
    blend_dir = r"d:\projects\Pirate_BR\PirateGame\Art\Blender\Shark"
    os.makedirs(blend_dir, exist_ok=True)
    blend_path = os.path.join(blend_dir, "Shark.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)

    # 19. Export .fbx
    fbx_dir = r"d:\projects\Pirate_BR\PirateGame\Assets\Models\Shark"
    os.makedirs(fbx_dir, exist_ok=True)
    fbx_path = os.path.join(fbx_dir, "Shark.fbx")

    bpy.ops.object.select_all(action='DESELECT')
    arm_obj.select_set(True)
    obj_body.select_set(True)
    obj_jaw.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj

    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        axis_forward='-Z',
        axis_up='Y',
        apply_scale_options='FBX_SCALE_ALL',
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_step=1.0,
        add_leaf_bones=False
    )

    print("--- Polished Shark Build Finished Successfully ---")

    def mat_counts(obj):
        counts = {}
        for p in obj.data.polygons:
            counts[p.material_index] = counts.get(p.material_index, 0) + 1
        return counts

    return {
        "status": "success",
        "blend_path": blend_path,
        "fbx_path": fbx_path,
        "body_dimensions": [round(v, 2) for v in dim_body],
        "jaw_dimensions": [round(v, 2) for v in dim_jaw],
        "face_mats_body": mat_counts(obj_body),
        "face_mats_jaw": mat_counts(obj_jaw),
        "actions": [a.name for a in bpy.data.actions if "Shark" in a.name]
    }

result = build_megalodon()
