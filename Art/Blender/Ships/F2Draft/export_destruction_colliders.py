import bpy
import bmesh
from mathutils import Vector

def export_collision_meshes():
    name='F2_Destruction_Collision'
    old=bpy.data.collections.get(name)
    if old:
        for obj in list(old.objects): bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.collections.remove(old)
    collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
    objects=[]
    for source in bpy.data.collections['F2_Destruction_Editable'].objects:
        if source.type!='MESH' or '_Chunk' not in source.name: continue
        points=[source.matrix_world@Vector(point) for point in source.bound_box]
        low=Vector([min(v[i] for v in points) for i in range(3)])
        high=Vector([max(v[i] for v in points) for i in range(3)])
        bm=bmesh.new();bmesh.ops.create_cube(bm,size=1)
        for v in bm.verts:
            v.co=Vector([v.co[i]*(high[i]-low[i])+(high[i]+low[i])*.5 for i in range(3)])
        mesh=bpy.data.meshes.new('COL_'+source.name);bm.to_mesh(mesh);bm.free()
        obj=bpy.data.objects.new(mesh.name,mesh);collection.objects.link(obj);objects.append(obj)
    for obj in bpy.context.selected_objects:obj.select_set(False)
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath='C:/Users/K/Project/Assets/Models/Ships/MainShip/Destruction/MainShipDestructionCollision.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False)
    collection.hide_viewport=True;collection.hide_render=True
    bpy.ops.wm.save_as_mainfile(filepath='C:/Users/K/Project/Art/Blender/Ships/F2Draft/F2_Ship_Destruction.blend')
    return len(objects)

result={'collision_meshes':export_collision_meshes()}
