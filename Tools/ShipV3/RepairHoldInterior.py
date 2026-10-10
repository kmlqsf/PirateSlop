import bpy,bmesh,json,re,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

base=Path('D:/projects/Pirate_BR/PirateGame')
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text(encoding='utf-8'))
document=json.loads((folder.parent/'ShipV3.json').read_text(encoding='utf-8'))
root=bpy.data.objects['Ship_V3_Fitted_Root']
assert root.get('hold_interior_fit_version',0)<2, 'Interior already fitted'
floor=manifest['floor_z'];ceiling=8.85
hawse=bpy.data.objects['V9_Anchor_Chain_Swing_Pivot'].matrix_world.translation
pieces=[p for p in document['pieces'] if p['family']=='Hull']
objects=[bpy.data.objects[n] for p in pieces for n in [p['intact']]+p['fragment_names']]
canonical={m.name:m for m in bpy.data.materials}
removed_lips=0
for obj in objects:
    if obj.data.users>1:obj.data=obj.data.copy()
    bm=bmesh.new();bm.from_mesh(obj.data)
    lips=[face for face in bm.faces if any(edge.is_boundary for edge in face.edges) and any(len(edge.link_faces)>2 for edge in face.edges)]
    if lips:
        removed_lips+=len(lips)
        bmesh.ops.delete(bm,geom=lips,context='FACES_ONLY')
        loose=[v for v in bm.verts if not v.link_faces]
        if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
        bm.to_mesh(obj.data);obj.data.update()
    bm.free()

def world_normal(obj,normal):return (obj.matrix_world.to_3x3().inverted().transposed()@normal).normalized()
def toward(point):return Vector((-point.x,-point.y,0)).normalized()
def internal(obj,face,basis=None):
    c=obj.matrix_world@face.calc_center_median()
    if face.material_index<len(obj.data.materials):
        mat=obj.data.materials[face.material_index]
        if mat and 'FreshWood' in mat.name:return False
    return world_normal(obj,face.normal).dot(basis if basis is not None else toward(c))>(.8 if basis is not None else .16)

bases={}
for piece in pieces:
    obj=bpy.data.objects[piece['intact']]
    bm=bmesh.new();bm.from_mesh(obj.data)
    candidates=[f for f in bm.faces if internal(obj,f)]
    if candidates:bases[obj.name]=world_normal(obj,max(candidates,key=lambda f:f.calc_area()).normal)
    bm.free()

vertices=[];triangles=[]
for piece in pieces:
    obj=bpy.data.objects[piece['intact']];obj.data.calc_loop_triangles()
    if obj.name not in bases:continue
    offset=len(vertices);vertices.extend(obj.matrix_world@v.co for v in obj.data.vertices)
    for tri in obj.data.loop_triangles:
        face=obj.data.polygons[tri.polygon_index];c=obj.matrix_world@face.center
        if world_normal(obj,face.normal).dot(-bases[obj.name])>.8:
            triangles.append(tuple(offset+i for i in tri.vertices))
outer=BVHTree.FromPolygons(vertices,triangles,all_triangles=True)

modified=set(manifest['modified_objects']);changed=0;max_move=0
for piece in pieces:
    intact=bpy.data.objects[piece['intact']]
    if intact.name not in bases:continue
    basis=bases[intact.name]
    zs=[(intact.matrix_world@v.co).z for v in intact.data.vertices]
    if not zs or max(zs)<floor-.1 or min(zs)>ceiling:continue
    bm=bmesh.new();bm.from_mesh(intact.data)
    faces=[f for f in bm.faces if internal(intact,f,basis)]
    if not faces:bm.free();continue
    inner={v for f in faces for v in f.verts}
    normal=sum((world_normal(intact,f.normal)*f.calc_area() for f in faces),Vector()).normalized()
    tangent=Vector((-normal.y,normal.x,0)).normalized()
    positions=[intact.matrix_world@v.co for v in inner]
    umin=min(p.dot(tangent) for p in positions);umax=max(p.dot(tangent) for p in positions)
    zmin=min(p.z for p in positions);zmax=max(p.z for p in positions)
    bm.free()
    for name in [piece['intact']]+piece['fragment_names']:
        obj=bpy.data.objects[name]
        if not len(obj.data.polygons):continue
        bm=bmesh.new();bm.from_mesh(obj.data)
        mark=bm.faces.layers.int.get('HoldInnerSurface') or bm.faces.layers.int.new('HoldInnerSurface')
        selected=[f for f in bm.faces if internal(obj,f,basis)]
        for face in bm.faces:face[mark]=0
        for face in selected:face[mark]=1;face.smooth=True
        points={v for f in selected for v in f.verts}
        inverse=obj.matrix_world.inverted()
        for vertex in points:
            p=obj.matrix_world@vertex.co
            if not floor-.1<=p.z<=ceiling or (p-hawse).length<.38:continue
            delta=tangent*((p.dot(tangent)-(umin+umax)*.5)*.026/max(.025,(umax-umin)*.5))
            if abs(p.z-floor)>.003:delta.z=(p.z-(zmin+zmax)*.5)*.026/max(.025,(zmax-zmin)*.5)
            q=p+delta
            nearest=outer.find_nearest(q)
            if nearest[0] is not None:
                direction=Vector((nearest[1].x,nearest[1].y,0))
                if direction.length>.1:
                    direction.normalize()
                    hit=outer.ray_cast(q-direction*.12,direction,1.0)
                    if hit[0] is not None:
                        horizontal=max(.4,math.hypot(hit[1].x,hit[1].y))
                        fitted=hit[0]-direction*(.12/horizontal)
                        fitted.z=q.z
                        if (fitted-p).length<.105:q=fitted
            if (q-p).length>.105:q=p+(q-p).normalized()*.105
            max_move=max(max_move,(q-p).length)
            vertex.co=inverse@q
        bmesh.ops.triangulate(bm,faces=selected,quad_method='BEAUTY',ngon_method='BEAUTY')
        bm.normal_update();bm.to_mesh(obj.data);bm.free();obj.data.update()
        modified.add(name);changed+=1

wood=canonical['V4_Hull_Ship_Art_Hull_Honey_Oak_V0']
for support in manifest.get('supports',[]):
    for name in [support['intact']]+support['fragments']:
        obj=bpy.data.objects[name]
        obj.data.materials.clear();obj.data.materials.append(wood)
        uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
        origin=obj.matrix_world.translation
        for polygon in obj.data.polygons:
            polygon.material_index=0
            for index in polygon.loop_indices:
                point=obj.matrix_world@obj.data.vertices[obj.data.loops[index].vertex_index].co-origin
                axis=point.y if abs(polygon.normal.x)>.5 else point.x
                uv.data[index].uv=((point.z+(ceiling-floor)*.5)*.28,axis*.6+.35)
        obj.data.update()

manifest['modified_objects']=sorted(modified)
manifest['version']=13
manifest['end_seams_version']=3
manifest['hold_interior_fit_version']=2
root['bilge_deck_version']=13;root['end_seams_version']=3;root['hold_interior_fit_version']=2
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.context.view_layer.update()
result={'version':13,'meshes':changed,'max_inner_vertex_move':max_move,'beam_material':wood.name,'removed_overlay_lips':removed_lips}
