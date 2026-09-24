import bpy
import bmesh
import math
import os
import shutil
from mathutils import Vector, Matrix, Euler

OUTPUT_DIR = r"d:\projects\Pirate_BR\PirateGame\Art\Blender\Whale\HarpoonConcepts"
ARTIFACT_DIR = r"C:\Users\pipai\.gemini\antigravity\brain\d6d7b54b-5b92-4654-b412-0bc81b3387de"

os.makedirs(OUTPUT_DIR, exist_ok=True)
os.makedirs(ARTIFACT_DIR, exist_ok=True)

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

# Надежные генераторы мешей вдоль оси Y без матричных ошибок
def add_frustum_y(bm, y1, y2, r1, r2, segments=8, rot_offset=0.0):
    verts_start = []
    verts_end = []
    for i in range(segments):
        angle = (2 * math.pi * i / segments) + rot_offset
        ca = math.cos(angle)
        sa = math.sin(angle)
        verts_start.append(bm.verts.new(Vector((ca * r1, y1, sa * r1))))
        verts_end.append(bm.verts.new(Vector((ca * r2, y2, sa * r2))))

    for i in range(segments):
        i_next = (i + 1) % segments
        bm.faces.new((verts_start[i], verts_start[i_next], verts_end[i_next], verts_end[i]))

    bm.faces.new(reversed(verts_start))
    bm.faces.new(verts_end)
    return verts_start, verts_end

def add_box_at(bm, center, size, rot_euler=Euler((0, 0, 0))):
    mat = Matrix.Translation(center) @ rot_euler.to_matrix().to_4x4() @ Matrix.Scale(size[0], 4, Vector((1, 0, 0))) @ Matrix.Scale(size[1], 4, Vector((0, 1, 0))) @ Matrix.Scale(size[2], 4, Vector((0, 0, 1)))
    bmesh.ops.create_cube(bm, size=1.0, matrix=mat)

def add_torus_y(bm, center_y, major_r, minor_r, seg_u=12, seg_v=6, offset_x=0.0, offset_z=0.0):
    verts_grid = []
    for i in range(seg_u):
        u = 2 * math.pi * i / seg_u
        row = []
        for j in range(seg_v):
            v = 2 * math.pi * j / seg_v
            # Торус лежит в плоскости X-Z, ось отверстия по Y
            r = major_r + minor_r * math.cos(v)
            x = offset_x + r * math.cos(u)
            z = offset_z + r * math.sin(u)
            y = center_y + minor_r * math.sin(v)
            row.append(bm.verts.new(Vector((x, y, z))))
        verts_grid.append(row)
    for i in range(seg_u):
        i_next = (i + 1) % seg_u
        for j in range(seg_v):
            j_next = (j + 1) % seg_v
            bm.faces.new((verts_grid[i][j], verts_grid[i_next][j], verts_grid[i_next][j_next], verts_grid[i][j_next]))

def add_ring_side(bm, center, major_r, minor_r, seg_u=10, seg_v=6):
    # Кольцо в плоскости Y-Z (ось отверстия по X)
    verts_grid = []
    for i in range(seg_u):
        u = 2 * math.pi * i / seg_u
        row = []
        for j in range(seg_v):
            v = 2 * math.pi * j / seg_v
            r = major_r + minor_r * math.cos(v)
            y = center[1] + r * math.cos(u)
            z = center[2] + r * math.sin(u)
            x = center[0] + minor_r * math.sin(v)
            row.append(bm.verts.new(Vector((x, y, z))))
        verts_grid.append(row)
    for i in range(seg_u):
        i_next = (i + 1) % seg_u
        for j in range(seg_v):
            j_next = (j + 1) % seg_v
            bm.faces.new((verts_grid[i][j], verts_grid[i_next][j], verts_grid[i_next][j_next], verts_grid[i][j_next]))

def setup_materials():
    mats = {}
    
    # 1. Кованое темное железо
    m_iron = bpy.data.materials.new(name="Mat_HarpoonIron")
    bsdf = m_iron.node_tree.nodes.get("Principled BSDF") or m_iron.node_tree.nodes.get("Принципиальный BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.13, 0.15, 0.17, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.50
        bsdf.inputs['Metallic'].default_value = 0.85
    mats['iron'] = m_iron

    # 2. Серо-стальной металл (наконечник)
    m_steel = bpy.data.materials.new(name="Mat_HarpoonSteel")
    bsdf = m_steel.node_tree.nodes.get("Principled BSDF") or m_steel.node_tree.nodes.get("Принципиальный BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.44, 0.48, 0.52, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.38
        bsdf.inputs['Metallic'].default_value = 0.90
    mats['steel'] = m_steel

    # 3. Темный мореный дуб (внешнее древко)
    m_wood = bpy.data.materials.new(name="Mat_DarkWood")
    bsdf = m_wood.node_tree.nodes.get("Principled BSDF") or m_wood.node_tree.nodes.get("Принципиальный BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.24, 0.11, 0.045, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.85
        bsdf.inputs['Metallic'].default_value = 0.0
    mats['wood'] = m_wood

    # 4. Светлая сердцевина излома и щепки
    m_splinter = bpy.data.materials.new(name="Mat_SplinterWood")
    bsdf = m_splinter.node_tree.nodes.get("Principled BSDF") or m_splinter.node_tree.nodes.get("Принципиальный BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.68, 0.50, 0.30, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.90
        bsdf.inputs['Metallic'].default_value = 0.0
    mats['splinter'] = m_splinter

    # 5. Пеньковый канат
    m_rope = bpy.data.materials.new(name="Mat_HempRope")
    bsdf = m_rope.node_tree.nodes.get("Principled BSDF") or m_rope.node_tree.nodes.get("Принципиальный BSDF")
    if bsdf:
        bsdf.inputs['Base Color'].default_value = (0.50, 0.37, 0.19, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.95
        bsdf.inputs['Metallic'].default_value = 0.0
    mats['rope'] = m_rope

    return mats

def make_flat_shaded(obj):
    if obj.type == 'MESH':
        for p in obj.data.polygons:
            p.use_smooth = False

def create_variant_1(col, mats):
    # =============================================================
    # ВАРИАНТ 1: Classic Barbed Whaler (Классический двухзубый гарпун)
    # =============================================================
    root = bpy.data.objects.new("Harpoon_Variant_1_Classic", None)
    col.objects.link(root)

    # 1. Деревянное древко: от Y = 0.0 до Y = -0.65 м (8 граней, радиус 0.044 м)
    mesh_shaft = bpy.data.meshes.new("Shaft_Mesh_V1")
    obj_shaft = bpy.data.objects.new("Shaft_V1", mesh_shaft)
    col.objects.link(obj_shaft)
    obj_shaft.parent = root
    bm = bmesh.new()
    add_frustum_y(bm, y1=-0.65, y2=0.02, r1=0.043, r2=0.044, segments=8)
    bm.to_mesh(mesh_shaft)
    bm.free()
    obj_shaft.data.materials.append(mats['wood'])

    # 2. Щепки излома древка: от Y = -0.65 до Y = -1.05 м
    mesh_sp = bpy.data.meshes.new("Splinters_Mesh_V1")
    obj_sp = bpy.data.objects.new("Splinters_V1", mesh_sp)
    col.objects.link(obj_sp)
    obj_sp.parent = root
    bm = bmesh.new()

    base_y = -0.65
    splinter_data = [
        (20,  0.38, 0.024),
        (70,  0.22, 0.018),
        (125, 0.44, 0.026),
        (180, 0.28, 0.020),
        (230, 0.41, 0.025),
        (280, 0.26, 0.019),
        (330, 0.35, 0.022)
    ]
    for ang, slen, sw in splinter_data:
        rad = math.radians(ang)
        r_base = 0.038
        bx = math.cos(rad) * r_base
        bz = math.sin(rad) * r_base

        # Основание щепки на срезе
        v0 = bm.verts.new(Vector((bx - sw*0.6, base_y, bz - sw*0.3)))
        v1 = bm.verts.new(Vector((bx + sw*0.6, base_y, bz - sw*0.3)))
        v2 = bm.verts.new(Vector((bx, base_y, bz + sw*0.7)))

        # Острие щепки (отклоняется наружу от удара)
        tx = bx * 1.35
        tz = bz * 1.35
        ty = base_y - slen
        v_tip = bm.verts.new(Vector((tx, ty, tz)))

        bm.faces.new((v0, v1, v_tip))
        bm.faces.new((v1, v2, v_tip))
        bm.faces.new((v2, v0, v_tip))
        bm.faces.new((v2, v1, v0))

    # Центральное разорванное ядро излома
    core_verts = []
    for i in range(6):
        a = 2 * math.pi * i / 6
        core_verts.append(bm.verts.new(Vector((math.cos(a)*0.025, base_y, math.sin(a)*0.025))))
    core_tip = bm.verts.new(Vector((0, base_y - 0.18, 0)))
    for i in range(6):
        i_n = (i + 1) % 6
        bm.faces.new((core_verts[i], core_verts[i_n], core_tip))
    bm.faces.new(reversed(core_verts))

    bm.to_mesh(mesh_sp)
    bm.free()
    obj_sp.data.materials.append(mats['splinter'])

    # 3. Железная муфта (Socket) и стержень (Shank)
    mesh_iron = bpy.data.meshes.new("Iron_Mesh_V1")
    obj_iron = bpy.data.objects.new("Iron_V1", mesh_iron)
    col.objects.link(obj_iron)
    obj_iron.parent = root
    bm = bmesh.new()

    # Основание муфты: утолщенный буртик от Y = -0.01 до Y = +0.03
    add_frustum_y(bm, y1=-0.01, y2=0.03, r1=0.058, r2=0.056, segments=8)
    # Коническая муфта от Y = 0.03 до Y = 0.22 (сужается к стержню)
    add_frustum_y(bm, y1=0.03, y2=0.22, r1=0.056, r2=0.032, segments=8)
    # Длинный граненый стержень от Y = 0.22 до Y = 0.62 (6 граней, R = 0.022)
    add_frustum_y(bm, y1=0.22, y2=0.62, r1=0.022, r2=0.020, segments=6)
    # Кольцо для троса сбоку на муфте (в Y = 0.09)
    add_ring_side(bm, center=(0.065, 0.09, 0.0), major_r=0.042, minor_r=0.011, seg_u=10, seg_v=6)

    bm.to_mesh(mesh_iron)
    bm.free()
    obj_iron.data.materials.append(mats['iron'])

    # 4. Наконечник гарпуна (Tip): от Y = 0.62 до Y = 1.08
    mesh_tip = bpy.data.meshes.new("Tip_Mesh_V1")
    obj_tip = bpy.data.objects.new("Tip_V1", mesh_tip)
    col.objects.link(obj_tip)
    obj_tip.parent = root
    bm = bmesh.new()

    v_tip = bm.verts.new(Vector((0.0, 1.08, 0.0)))
    v_ridge_top = bm.verts.new(Vector((0.0, 0.82, 0.026)))
    v_ridge_bot = bm.verts.new(Vector((0.0, 0.82, -0.026)))
    v_base_top = bm.verts.new(Vector((0.0, 0.62, 0.020)))
    v_base_bot = bm.verts.new(Vector((0.0, 0.62, -0.020)))

    v_left_barb = bm.verts.new(Vector((-0.14, 0.70, 0.0)))
    v_left_notch = bm.verts.new(Vector((-0.035, 0.68, 0.0)))
    v_right_barb = bm.verts.new(Vector((0.14, 0.70, 0.0)))
    v_right_notch = bm.verts.new(Vector((0.035, 0.68, 0.0)))

    # Передние грани
    bm.faces.new((v_tip, v_left_barb, v_ridge_top))
    bm.faces.new((v_tip, v_ridge_top, v_right_barb))
    bm.faces.new((v_tip, v_ridge_bot, v_left_barb))
    bm.faces.new((v_tip, v_right_barb, v_ridge_bot))

    # Задние грани (бородки)
    bm.faces.new((v_left_barb, v_left_notch, v_base_top, v_ridge_top))
    bm.faces.new((v_right_barb, v_ridge_top, v_base_top, v_right_notch))
    bm.faces.new((v_left_barb, v_ridge_bot, v_base_bot, v_left_notch))
    bm.faces.new((v_right_barb, v_right_notch, v_base_bot, v_ridge_bot))

    # Торцевые грани
    bm.faces.new((v_left_barb, v_ridge_top, v_ridge_bot))
    bm.faces.new((v_right_barb, v_ridge_bot, v_ridge_top))
    bm.faces.new((v_base_top, v_left_notch, v_base_bot))
    bm.faces.new((v_base_top, v_base_bot, v_right_notch))

    bm.to_mesh(mesh_tip)
    bm.free()
    obj_tip.data.materials.append(mats['steel'])

    # 5. Пеньковый канат (Rope): 3 витка на муфте + узел + свисающий оборванный трос
    mesh_rope = bpy.data.meshes.new("Rope_Mesh_V1")
    obj_rope = bpy.data.objects.new("Rope_V1", mesh_rope)
    col.objects.link(obj_rope)
    obj_rope.parent = root
    bm = bmesh.new()

    for i, ry in enumerate([0.05, 0.08, 0.11]):
        add_torus_y(bm, center_y=ry, major_r=0.056 + i*0.002, minor_r=0.015, seg_u=10, seg_v=6)
    
    # Узел
    add_box_at(bm, center=Vector((0.065, 0.08, -0.01)), size=(0.046, 0.046, 0.046), rot_euler=Euler((0, 0, math.radians(35))))

    # Свисающий хвост троса
    curve_pts = [
        Vector((0.065, 0.08, -0.02)),
        Vector((0.090, 0.03, -0.07)),
        Vector((0.115, -0.05, -0.13)),
        Vector((0.130, -0.15, -0.20)),
        Vector((0.120, -0.27, -0.27))
    ]
    for p1, p2 in zip(curve_pts[:-1], curve_pts[1:]):
        delta = p2 - p1
        mid = (p1 + p2) * 0.5
        rot = Vector((0, 0, 1)).rotation_difference(delta.normalized()).to_matrix().to_4x4()
        bmesh.ops.create_cone(
            bm, cap_ends=True, cap_tris=False, segments=6, radius1=0.016, radius2=0.016, depth=delta.length,
            matrix=Matrix.Translation(mid) @ rot
        )
    # Растрепанный конец (щетина пеньки)
    last_pt = curve_pts[-1]
    for s_dir, s_rad in [(Vector((0.02, -0.05, -0.06)), 0.007),
                         (Vector((-0.025, -0.06, -0.05)), 0.006),
                         (Vector((0.01, -0.07, -0.08)), 0.008)]:
        end_pt = last_pt + s_dir
        delta = end_pt - last_pt
        mid = (last_pt + end_pt) * 0.5
        rot = Vector((0, 0, 1)).rotation_difference(delta.normalized()).to_matrix().to_4x4()
        bmesh.ops.create_cone(
            bm, cap_ends=True, cap_tris=False, segments=5, radius1=s_rad, radius2=s_rad, depth=delta.length,
            matrix=Matrix.Translation(mid) @ rot
        )

    bm.to_mesh(mesh_rope)
    bm.free()
    obj_rope.data.materials.append(mats['rope'])

    for obj in [obj_shaft, obj_sp, obj_iron, obj_tip, obj_rope]:
        make_flat_shaded(obj)

    return root

def create_variant_2(col, mats):
    # =============================================================
    # ВАРИАНТ 2: Heavy Toggle Harpoon (Шарнирный гарпун с массивным серповидным крюком)
    # =============================================================
    root = bpy.data.objects.new("Harpoon_Variant_2_Toggle", None)
    col.objects.link(root)

    # 1. Деревянное древко: от Y = 0.0 до Y = -0.55 м (толще, 8 граней, R = 0.048)
    mesh_shaft = bpy.data.meshes.new("Shaft_Mesh_V2")
    obj_shaft = bpy.data.objects.new("Shaft_V2", mesh_shaft)
    col.objects.link(obj_shaft)
    obj_shaft.parent = root
    bm = bmesh.new()
    add_frustum_y(bm, y1=-0.55, y2=0.02, r1=0.047, r2=0.048, segments=8)
    bm.to_mesh(mesh_shaft)
    bm.free()
    obj_shaft.data.materials.append(mats['wood'])

    # 2. Диагональный раскол со свирепым отщепом древесины
    mesh_sp = bpy.data.meshes.new("Splinters_Mesh_V2")
    obj_sp = bpy.data.objects.new("Splinters_V2", mesh_sp)
    col.objects.link(obj_sp)
    obj_sp.parent = root
    bm = bmesh.new()

    base_y = -0.55
    # Огромный диагональный клин-отщеп (длина 45 см)
    v0 = bm.verts.new(Vector((-0.045, base_y, -0.03)))
    v1 = bm.verts.new(Vector((0.045, base_y, -0.03)))
    v2 = bm.verts.new(Vector((0.025, base_y, 0.045)))
    v3 = bm.verts.new(Vector((-0.025, base_y, 0.045)))
    v_main_tip = bm.verts.new(Vector((0.015, base_y - 0.45, -0.05)))

    bm.faces.new((v0, v1, v_main_tip))
    bm.faces.new((v1, v2, v_main_tip))
    bm.faces.new((v2, v3, v_main_tip))
    bm.faces.new((v3, v0, v_main_tip))
    bm.faces.new((v3, v2, v1, v0))

    # Боковые расщепы
    side_splinters = [
        (Vector((-0.04, base_y, 0.02)), Vector((-0.05, base_y - 0.30, 0.035)), 0.018),
        (Vector((0.04, base_y, 0.02)), Vector((0.05, base_y - 0.24, 0.035)), 0.016),
        (Vector((0.0, base_y, 0.042)), Vector((0.0, base_y - 0.34, 0.055)), 0.020)
    ]
    for b_pos, t_pos, w in side_splinters:
        sv0 = bm.verts.new(b_pos + Vector((-w*0.5, 0, -w*0.5)))
        sv1 = bm.verts.new(b_pos + Vector((w*0.5, 0, -w*0.5)))
        sv2 = bm.verts.new(b_pos + Vector((0, 0, w*0.7)))
        sv_tip = bm.verts.new(t_pos)
        bm.faces.new((sv0, sv1, sv_tip))
        bm.faces.new((sv1, sv2, sv_tip))
        bm.faces.new((sv2, sv0, sv_tip))
        bm.faces.new((sv2, sv1, sv0))

    bm.to_mesh(mesh_sp)
    bm.free()
    obj_sp.data.materials.append(mats['splinter'])

    # 3. Железная муфта с заклепками и поворотной проушиной
    mesh_iron = bpy.data.meshes.new("Iron_Mesh_V2")
    obj_iron = bpy.data.objects.new("Iron_V2", mesh_iron)
    col.objects.link(obj_iron)
    obj_iron.parent = root
    bm = bmesh.new()

    # Муфта Y = -0.01 .. 0.22 (8 граней, R = 0.058)
    add_frustum_y(bm, y1=-0.01, y2=0.22, r1=0.060, r2=0.040, segments=8)
    # Квадратные заклепки
    for rot_z in [0, 90, 180, 270]:
        rad_z = math.radians(rot_z)
        px = math.cos(rad_z) * 0.058
        pz = math.sin(rad_z) * 0.058
        add_box_at(bm, center=Vector((px, 0.06, pz)), size=(0.016, 0.016, 0.016))
        add_box_at(bm, center=Vector((px, 0.14, pz)), size=(0.016, 0.016, 0.016))

    # Стержень Y = 0.22 .. 0.60
    add_frustum_y(bm, y1=0.22, y2=0.60, r1=0.026, r2=0.024, segments=6)
    # Шарнирная проушина в Y = 0.60
    add_box_at(bm, center=Vector((0.0, 0.60, 0.0)), size=(0.045, 0.045, 0.038))

    bm.to_mesh(mesh_iron)
    bm.free()
    obj_iron.data.materials.append(mats['iron'])

    # 4. Асимметричный поворотный наконечник с массивным серповидным зубом
    mesh_tip = bpy.data.meshes.new("Tip_Mesh_V2")
    obj_tip = bpy.data.objects.new("Tip_V2", mesh_tip)
    col.objects.link(obj_tip)
    obj_tip.parent = root
    bm = bmesh.new()

    t_tip = bm.verts.new(Vector((-0.03, 1.10, 0.0)))
    t_center_top = bm.verts.new(Vector((0.0, 0.86, 0.028)))
    t_center_bot = bm.verts.new(Vector((0.0, 0.86, -0.028)))
    
    # Огромный серповидный крюк справа (+X)
    t_hook_tip = bm.verts.new(Vector((0.18, 0.66, 0.0)))
    t_hook_inner = bm.verts.new(Vector((0.05, 0.74, 0.0)))

    # Скалывающая грань слева (-X)
    t_left_edge = bm.verts.new(Vector((-0.08, 0.80, 0.0)))
    t_base_top = bm.verts.new(Vector((0.0, 0.60, 0.024)))
    t_base_bot = bm.verts.new(Vector((0.0, 0.60, -0.024)))

    bm.faces.new((t_tip, t_left_edge, t_center_top))
    bm.faces.new((t_tip, t_center_top, t_hook_inner))
    bm.faces.new((t_tip, t_center_bot, t_left_edge))
    bm.faces.new((t_tip, t_hook_inner, t_center_bot))

    bm.faces.new((t_hook_inner, t_hook_tip, t_center_top))
    bm.faces.new((t_hook_inner, t_center_bot, t_hook_tip))

    bm.faces.new((t_left_edge, t_base_top, t_center_top))
    bm.faces.new((t_left_edge, t_center_bot, t_base_bot))

    bm.faces.new((t_center_top, t_base_top, t_hook_inner))
    bm.faces.new((t_center_bot, t_hook_inner, t_base_bot))

    bm.faces.new((t_hook_tip, t_hook_inner, t_base_top))
    bm.faces.new((t_hook_tip, t_base_bot, t_hook_inner))

    bm.to_mesh(mesh_tip)
    bm.free()
    obj_tip.data.materials.append(mats['steel'])

    # 5. Канат: Перекрестная обмотка крестом + свисающая петля
    mesh_rope = bpy.data.meshes.new("Rope_Mesh_V2")
    obj_rope = bpy.data.objects.new("Rope_V2", mesh_rope)
    col.objects.link(obj_rope)
    obj_rope.parent = root
    bm = bmesh.new()

    for ry in [0.03, 0.07, 0.11, 0.15]:
        add_torus_y(bm, center_y=ry, major_r=0.060, minor_r=0.014, seg_u=10, seg_v=6)

    # Диагональный перехлест крестом
    add_box_at(bm, center=Vector((0.06, 0.09, 0.0)), size=(0.024, 0.16, 0.024), rot_euler=Euler((0, 0, math.radians(35))))
    add_box_at(bm, center=Vector((0.06, 0.09, 0.0)), size=(0.024, 0.16, 0.024), rot_euler=Euler((0, 0, math.radians(-35))))

    # Свисающая петля (Bight)
    loop_pts = [
        Vector((0.06, 0.09, -0.02)),
        Vector((0.10, 0.03, -0.08)),
        Vector((0.12, -0.08, -0.16)),
        Vector((0.10, -0.20, -0.24)),
        Vector((0.06, -0.28, -0.28)),
        Vector((0.01, -0.32, -0.24)),
        Vector((0.03, -0.26, -0.18))
    ]
    for p1, p2 in zip(loop_pts[:-1], loop_pts[1:]):
        delta = p2 - p1
        mid = (p1 + p2) * 0.5
        rot = Vector((0, 0, 1)).rotation_difference(delta.normalized()).to_matrix().to_4x4()
        bmesh.ops.create_cone(
            bm, cap_ends=True, cap_tris=False, segments=6, radius1=0.014, radius2=0.014, depth=delta.length,
            matrix=Matrix.Translation(mid) @ rot
        )

    bm.to_mesh(mesh_rope)
    bm.free()
    obj_rope.data.materials.append(mats['rope'])

    for obj in [obj_shaft, obj_sp, obj_iron, obj_tip, obj_rope]:
        make_flat_shaded(obj)

    return root

def create_variant_3(col, mats):
    # =============================================================
    # ВАРИАНТ 3: Cruel Multi-Barbed Harpoon (Шипастый копейный гарпун с ланжетами)
    # =============================================================
    root = bpy.data.objects.new("Harpoon_Variant_3_MultiBarbed", None)
    col.objects.link(root)

    # 1. Деревянное древко: от Y = 0.0 до Y = -0.75 м (8 граней, R = 0.042)
    mesh_shaft = bpy.data.meshes.new("Shaft_Mesh_V3")
    obj_shaft = bpy.data.objects.new("Shaft_V3", mesh_shaft)
    col.objects.link(obj_shaft)
    obj_shaft.parent = root
    bm = bmesh.new()
    add_frustum_y(bm, y1=-0.75, y2=0.02, r1=0.041, r2=0.042, segments=8)
    bm.to_mesh(mesh_shaft)
    bm.free()
    obj_shaft.data.materials.append(mats['wood'])

    # 2. Размочаленный излом ("метла" из волокон): от Y = -0.75 до Y = -1.10 м
    mesh_sp = bpy.data.meshes.new("Splinters_Mesh_V3")
    obj_sp = bpy.data.objects.new("Splinters_V3", mesh_sp)
    col.objects.link(obj_sp)
    obj_sp.parent = root
    bm = bmesh.new()

    base_y = -0.75
    num_splinters = 12
    for i in range(num_splinters):
        ang = (360.0 / num_splinters) * i + (i * 7 % 13)
        rad = math.radians(ang)
        r_base = 0.015 + (i % 3) * 0.01
        slen = 0.18 + ((i * 17) % 25) * 0.012
        sw = 0.012 + (i % 2) * 0.006

        bx = math.cos(rad) * r_base
        bz = math.sin(rad) * r_base

        v0 = bm.verts.new(Vector((bx - sw*0.5, base_y, bz - sw*0.3)))
        v1 = bm.verts.new(Vector((bx + sw*0.5, base_y, bz - sw*0.3)))
        v2 = bm.verts.new(Vector((bx, base_y, bz + sw*0.6)))

        tx = bx * 1.65 + math.cos(rad) * 0.02
        tz = bz * 1.65 + math.sin(rad) * 0.02
        ty = base_y - slen
        v_tip = bm.verts.new(Vector((tx, ty, tz)))

        bm.faces.new((v0, v1, v_tip))
        bm.faces.new((v1, v2, v_tip))
        bm.faces.new((v2, v0, v_tip))
        bm.faces.new((v2, v1, v0))

    bm.to_mesh(mesh_sp)
    bm.free()
    obj_sp.data.materials.append(mats['splinter'])

    # 3. Железная муфта + ланжеты вдоль древка назад на 35 см
    mesh_iron = bpy.data.meshes.new("Iron_Mesh_V3")
    obj_iron = bpy.data.objects.new("Iron_V3", mesh_iron)
    col.objects.link(obj_iron)
    obj_iron.parent = root
    bm = bmesh.new()

    # Муфта Y = 0.0 .. 0.15
    add_frustum_y(bm, y1=0.0, y2=0.15, r1=0.052, r2=0.036, segments=8)
    # Две продольные железные полосы (ланжеты) назад вдоль древка на 35 см
    for side in [-1, 1]:
        add_box_at(bm, center=Vector((side * 0.044, -0.15, 0.0)), size=(0.008, 0.35, 0.026))
        for py in [-0.05, -0.15, -0.25]:
            add_box_at(bm, center=Vector((side * 0.049, py, 0.0)), size=(0.014, 0.014, 0.014))

    # Железный бандаж в Y = -0.20
    add_frustum_y(bm, y1=-0.22, y2=-0.18, r1=0.048, r2=0.048, segments=8)

    # 4-гранный кованый стержень Y = 0.15 .. 0.62
    add_frustum_y(bm, y1=0.15, y2=0.62, r1=0.024, r2=0.022, segments=4)

    bm.to_mesh(mesh_iron)
    bm.free()
    obj_iron.data.materials.append(mats['iron'])

    # 4. Хищный многошипый наконечник с двумя парами зазубрин
    mesh_tip = bpy.data.meshes.new("Tip_Mesh_V3")
    obj_tip = bpy.data.objects.new("Tip_V3", mesh_tip)
    col.objects.link(obj_tip)
    obj_tip.parent = root
    bm = bmesh.new()

    v_tip = bm.verts.new(Vector((0.0, 1.15, 0.0)))
    v_top = bm.verts.new(Vector((0.0, 0.95, 0.025)))
    v_bot = bm.verts.new(Vector((0.0, 0.95, -0.025)))

    # Верхние шипы
    v_barb1_l = bm.verts.new(Vector((-0.12, 0.88, 0.0)))
    v_barb1_r = bm.verts.new(Vector((0.12, 0.88, 0.0)))
    v_notch1_l = bm.verts.new(Vector((-0.034, 0.86, 0.0)))
    v_notch1_r = bm.verts.new(Vector((0.034, 0.86, 0.0)))

    # Нижние шипы
    v_barb2_l = bm.verts.new(Vector((-0.09, 0.74, 0.0)))
    v_barb2_r = bm.verts.new(Vector((0.09, 0.74, 0.0)))
    v_notch2_l = bm.verts.new(Vector((-0.030, 0.72, 0.0)))
    v_notch2_r = bm.verts.new(Vector((0.030, 0.72, 0.0)))

    v_base_top = bm.verts.new(Vector((0.0, 0.62, 0.022)))
    v_base_bot = bm.verts.new(Vector((0.0, 0.62, -0.022)))

    bm.faces.new((v_tip, v_barb1_l, v_top))
    bm.faces.new((v_tip, v_top, v_barb1_r))
    bm.faces.new((v_tip, v_bot, v_barb1_l))
    bm.faces.new((v_tip, v_barb1_r, v_bot))

    bm.faces.new((v_barb1_l, v_notch1_l, v_top))
    bm.faces.new((v_barb1_r, v_top, v_notch1_r))
    bm.faces.new((v_barb1_l, v_bot, v_notch1_l))
    bm.faces.new((v_barb1_r, v_notch1_r, v_bot))

    bm.faces.new((v_notch1_l, v_barb2_l, v_top))
    bm.faces.new((v_notch1_r, v_top, v_barb2_r))
    bm.faces.new((v_notch1_l, v_bot, v_barb2_l))
    bm.faces.new((v_notch1_r, v_barb2_r, v_bot))

    bm.faces.new((v_barb2_l, v_notch2_l, v_base_top, v_top))
    bm.faces.new((v_barb2_r, v_top, v_base_top, v_notch2_r))
    bm.faces.new((v_barb2_l, v_bot, v_base_bot, v_notch2_l))
    bm.faces.new((v_barb2_r, v_notch2_r, v_base_bot, v_bot))

    bm.faces.new((v_base_top, v_notch2_l, v_base_bot))
    bm.faces.new((v_base_top, v_base_bot, v_notch2_r))

    bm.to_mesh(mesh_tip)
    bm.free()
    obj_tip.data.materials.append(mats['steel'])

    # 5. Канат: Обмотка муфты + смоленый узел + короткий свисающий трос
    mesh_rope = bpy.data.meshes.new("Rope_Mesh_V3")
    obj_rope = bpy.data.objects.new("Rope_V3", mesh_rope)
    col.objects.link(obj_rope)
    obj_rope.parent = root
    bm = bmesh.new()

    for ry in [0.03, 0.07]:
        add_torus_y(bm, center_y=ry, major_r=0.054, minor_r=0.014, seg_u=10, seg_v=6)

    add_box_at(bm, center=Vector((-0.065, 0.05, 0.02)), size=(0.048, 0.048, 0.048), rot_euler=Euler((0, math.radians(40), 0)))
    
    pts_v3 = [
        Vector((-0.065, 0.05, 0.02)),
        Vector((-0.11, 0.01, 0.06)),
        Vector((-0.15, -0.06, 0.10)),
        Vector((-0.17, -0.16, 0.12)),
        Vector((-0.15, -0.26, 0.10))
    ]
    for p1, p2 in zip(pts_v3[:-1], pts_v3[1:]):
        delta = p2 - p1
        mid = (p1 + p2) * 0.5
        rot = Vector((0, 0, 1)).rotation_difference(delta.normalized()).to_matrix().to_4x4()
        bmesh.ops.create_cone(
            bm, cap_ends=True, cap_tris=False, segments=6, radius1=0.015, radius2=0.015, depth=delta.length,
            matrix=Matrix.Translation(mid) @ rot
        )

    bm.to_mesh(mesh_rope)
    bm.free()
    obj_rope.data.materials.append(mats['rope'])

    for obj in [obj_shaft, obj_sp, obj_iron, obj_tip, obj_rope]:
        make_flat_shaded(obj)

    return root

def setup_studio_and_render(variants):
    world = bpy.data.worlds.new("StudioWorld")
    bpy.context.scene.world = world
    bg_node = world.node_tree.nodes.get("Background") or world.node_tree.nodes.get("Фон")
    if bg_node:
        bg_node.inputs['Color'].default_value = (0.50, 0.52, 0.55, 1.0)
        bg_node.inputs['Strength'].default_value = 0.90

    # Поворачиваем каждый гарпун в студии под сочный диагональный угол 45 градусов
    for v in variants:
        v.rotation_euler = Euler((math.radians(20), math.radians(-32), math.radians(45)), 'XYZ')

    # Камера (Orthographic 3/4 Isometric View)
    cam_data = bpy.data.cameras.new("StudioCamera")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 1.75 # Крупнее, выразительнее
    cam_obj = bpy.data.objects.new("StudioCamera", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    bpy.context.scene.camera = cam_obj

    # Ракурс 3/4 изометрия
    cam_distance = 6.0
    elev = math.radians(26)
    azim = math.radians(38)
    cx = cam_distance * math.cos(elev) * math.sin(azim)
    cy = -cam_distance * math.cos(elev) * math.cos(azim)
    cz = cam_distance * math.sin(elev) + 0.05
    cam_obj.location = Vector((cx, cy, cz))

    target = Vector((0, 0.0, 0))
    direction = target - cam_obj.location
    rot_quat = direction.to_track_quat('-Z', 'Y')
    cam_obj.rotation_euler = rot_quat.to_euler()

    # Студийное 3-точечное освещение
    # 1. Key Light
    key_data = bpy.data.lights.new("KeyLight", type='SUN')
    key_data.energy = 3.6
    key_data.color = (1.0, 0.98, 0.95)
    key_obj = bpy.data.objects.new("KeyLight", key_data)
    bpy.context.collection.objects.link(key_obj)
    key_obj.rotation_euler = Euler((math.radians(45), math.radians(25), math.radians(-25)), 'XYZ')

    # 2. Fill Light
    fill_data = bpy.data.lights.new("FillLight", type='SUN')
    fill_data.energy = 1.8
    fill_data.color = (0.86, 0.92, 1.0)
    fill_obj = bpy.data.objects.new("FillLight", fill_data)
    bpy.context.collection.objects.link(fill_obj)
    fill_obj.rotation_euler = Euler((math.radians(35), math.radians(-45), math.radians(65)), 'XYZ')

    # 3. Rim Light (контурная подсветка граней)
    rim_data = bpy.data.lights.new("RimLight", type='SUN')
    rim_data.energy = 3.2
    rim_data.color = (0.95, 0.96, 1.0)
    rim_obj = bpy.data.objects.new("RimLight", rim_data)
    bpy.context.collection.objects.link(rim_obj)
    rim_obj.rotation_euler = Euler((math.radians(-50), math.radians(25), math.radians(155)), 'XYZ')

    scene = bpy.context.scene
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.film_transparent = False
    scene.render.engine = 'BLENDER_EEVEE'

    rendered_images = []

    names = [
        ("harpoon_variant_1_classic.png", variants[0]),
        ("harpoon_variant_2_toggle.png", variants[1]),
        ("harpoon_variant_3_multibarbed.png", variants[2])
    ]

    for fname, var_root in names:
        for v in variants:
            v.hide_render = True
            for child in v.children:
                child.hide_render = True
        
        var_root.hide_render = False
        for child in var_root.children:
            child.hide_render = False

        orig_loc = var_root.location.copy()
        var_root.location = Vector((0, 0, 0))

        out_path = os.path.join(OUTPUT_DIR, fname)
        scene.render.filepath = out_path
        bpy.ops.render.render(write_still=True)
        rendered_images.append(out_path)

        art_path = os.path.join(ARTIFACT_DIR, fname)
        shutil.copy(out_path, art_path)
        print(f"Rendered: {out_path} -> {art_path}")
        var_root.location = orig_loc

    # Рендер сравнения (все 3 рядом)
    cam_data.ortho_scale = 3.4
    variants[0].location = Vector((-0.80, 0, 0))
    variants[1].location = Vector((0.0, 0, 0))
    variants[2].location = Vector((0.80, 0, 0))

    for v in variants:
        v.hide_render = False
        for child in v.children:
            child.hide_render = False

    comp_fname = "harpoon_comparison_all.png"
    comp_path = os.path.join(OUTPUT_DIR, comp_fname)
    scene.render.filepath = comp_path
    bpy.ops.render.render(write_still=True)
    rendered_images.append(comp_path)
    shutil.copy(comp_path, os.path.join(ARTIFACT_DIR, comp_fname))
    print(f"Rendered comparison: {comp_path}")

    blend_path = os.path.join(OUTPUT_DIR, "HarpoonConcepts.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"Saved blend file: {blend_path}")

    return rendered_images, blend_path

def main():
    reset_scene()
    mats = setup_materials()

    col = bpy.context.collection
    v1 = create_variant_1(col, mats)
    v2 = create_variant_2(col, mats)
    v3 = create_variant_3(col, mats)

    setup_studio_and_render([v1, v2, v3])
    print("ALL DONE PERFECTLY!")

if __name__ == '__main__':
    main()
