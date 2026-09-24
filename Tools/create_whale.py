import bpy
import bmesh
import random
import os

# 1. Clear existing objects
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# 2. Create base icosphere (subdivisions=3)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=1.0, location=(0, 0, 0))
whale = bpy.context.active_object
whale.name = 'DriftingWhale'

# 3. Scale along Y axis to make it elongated like a whale
whale.scale = (1.5, 3.5, 1.2)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# 4. Use bmesh to add noise (Displace) and flatten the top safely
bm = bmesh.new()
bm.from_mesh(whale.data)

for v in bm.verts:
    # Имитация неровностей (израненность кожи)
    v.co.x += random.uniform(-0.08, 0.08)
    v.co.y += random.uniform(-0.08, 0.08)
    v.co.z += random.uniform(-0.08, 0.08)
    
    # Срезаем верхушку для плоской палубы (вместо проблемного модификатора Boolean)
    if v.co.z > 0.4:
        v.co.z = 0.4

# Пересчитываем нормали после ручного изменения вершин
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(whale.data)
bm.free()

# 5. Shade flat для low-poly стиля
bpy.ops.object.shade_flat()

# Save file
save_path = r"D:\projects\Pirate_BR\PirateGame\Assets\Models\DriftingWhale.blend"
os.makedirs(os.path.dirname(save_path), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=save_path)
print(f"Saved safely to {save_path}")
