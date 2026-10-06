import bpy
import bmesh
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view
previous_scene=bpy.context.scene
scene=bpy.data.scenes['CoastalArchCReview']
bpy.context.window.scene=scene
body=next(o for o in scene.objects if '_FracturedStone' in o.name)
points=[body.matrix_world @ Vector(c) for c in body.bound_box]
low=Vector(tuple(min(p[i] for p in points) for i in range(3)))
high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
center=(low+high)*.5
extent=max(high-low)
target=center+Vector((0,0,9))
camera_data=bpy.data.cameras.new('ArchFragmentProjection')
camera=bpy.data.objects.new('ArchFragmentProjection',camera_data)
scene.collection.objects.link(camera)
camera_data.type='ORTHO'
camera_data.ortho_scale=extent*1.04
camera.location=target+Vector((.52,-1.55,.46))*extent
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.context.view_layer.update()
bm=bmesh.new()
bm.from_mesh(body.data)
bm.verts.ensure_lookup_table()
eligible=set()
seeds=set()
for vertex in bm.verts:
    world=body.matrix_world @ vertex.co
    projected=world_to_camera_view(scene,camera,world)
    x=projected.x*1600
    y=(1-projected.y)*1100
    if vertex.co.z<64 and 1040<x<1105 and 480<y<620:
        eligible.add(vertex)
        if 1060<x<1090 and y>515:seeds.add(vertex)
selected=set(seeds)
pending=list(seeds)
while pending:
    vertex=pending.pop()
    for edge in vertex.link_edges:
        other=edge.other_vert(vertex)
        if other in eligible and other not in selected:
            selected.add(other)
            pending.append(other)
faces={face for vertex in selected for face in vertex.link_faces}
edges={edge for face in faces for edge in face.edges}
vertices={vertex for face in faces for vertex in face.verts}
before=len(bm.faces)
cut=bmesh.ops.bisect_plane(bm,geom=list(faces|edges|vertices),dist=.0001,plane_co=Vector((0,0,64)),plane_no=Vector((0,0,1)),clear_inner=True,clear_outer=False)
cap_edges={item for item in cut['geom_cut'] if isinstance(item,bmesh.types.BMEdge) and item.is_valid and item.is_boundary and all(abs(v.co.z-64)<.002 for v in item.verts)}
cap_count=0
while cap_edges:
    component={cap_edges.pop()}
    pending=list(component)
    while pending:
        edge=pending.pop()
        linked={candidate for vertex in edge.verts for candidate in vertex.link_edges if candidate in cap_edges}
        cap_edges.difference_update(linked)
        component.update(linked)
        pending.extend(linked)
    ring={vertex for edge in component for vertex in edge.verts}
    span=max((max(v.co[i] for v in ring)-min(v.co[i] for v in ring)) for i in range(3))
    closed=all(sum(1 for edge in vertex.link_edges if edge in component)==2 for vertex in ring)
    if closed and span<16:
        filled=bmesh.ops.holes_fill(bm,edges=list(component),sides=0)
        cap_count+=len(filled['faces'])
bmesh.ops.delete(bm,geom=[vertex for vertex in bm.verts if not vertex.link_faces],context='VERTS')
bmesh.ops.dissolve_degenerate(bm,dist=.0001,edges=list(bm.edges))
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bmesh.ops.triangulate(bm,faces=list(bm.faces))
bm.to_mesh(body.data)
after=len(body.data.polygons)
bm.free()
body.data.update()
uv=body.data.uv_layers.active
for polygon in body.data.polygons:
    axis=max(range(3),key=lambda a:abs(polygon.normal[a]))
    for index in polygon.loop_indices:
        point=body.data.vertices[body.data.loops[index].vertex_index].co
        coordinate=(point.y,point.z) if axis==0 else (point.x,point.z) if axis==1 else (point.x,point.y)
        uv.data[index].uv=(coordinate[0]*.075,coordinate[1]*.075)
bpy.data.objects.remove(camera,do_unlink=True)
bpy.data.cameras.remove(camera_data)
save_path=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\SeaArch_Huge_A_CReview.blend'
bpy.ops.wm.save_as_mainfile(filepath=save_path,copy=True,compress=True)
bpy.context.window.scene=previous_scene
result={'projectionSeeds':len(seeds),'selectedVertices':len(selected),'selectedFaces':len(faces),'shortCapsFilled':cap_count,'stoneTrisBefore':before,'stoneTrisAfter':after,'saved':save_path,'activeSceneRestored':previous_scene.name}
