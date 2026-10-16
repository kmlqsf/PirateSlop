import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils import Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

base=Path(__file__).resolve().parents[2]
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text())
document=json.loads((folder.parent/'ShipV3.json').read_text())
root=bpy.data.objects['Ship_V3_Fitted_Root']
prior=manifest.get('appearance_lining',[])
prior_names={entry['name'] for entry in prior}
bpy.data.batch_remove(ids=[bpy.data.objects[entry['name']] for entry in prior if entry['name'] in bpy.data.objects and bpy.data.objects[entry['name']].get('appearance_parent')==entry['parent']])
manifest['modified_objects']=[name for name in manifest['modified_objects'] if name not in prior_names]
matrices={o.name:o.matrix_world.copy() for o in bpy.data.objects}
pieces=[p for p in document['pieces'] if (p['family']=='Hull' or 'Quarterdeck_Sidewall' in p['intact']) and (p['bounds_max'][2]>=5.05 or p['bounds_max'][1]>16)]
vertices=[];triangles=[];owners=[]
for index,piece in enumerate(pieces):
    obj=bpy.data.objects[piece['intact']]
    obj.data.calc_loop_triangles()
    offset=len(vertices)
    vertices.extend(matrices[obj.name]@v.co for v in obj.data.vertices)
    for triangle in obj.data.loop_triangles:
        triangles.append(tuple(offset+i for i in triangle.vertices));owners.append(index)
coverage_tree=BVHTree.FromPolygons(vertices,triangles,all_triangles=True)
reference=bpy.data.objects['V3_Rounded_Full_Hull']
reference.data.calc_loop_triangles()
raw_tree=BVHTree.FromPolygons([matrices[reference.name]@v.co for v in reference.data.vertices],[tuple(t.vertices) for t in reference.data.loop_triangles],all_triangles=True)
casings={p['intact']:p for p in document['pieces'] if '_Casing_' in p['intact']}
openings=[]
for name in casings:
    if not name.endswith('_Casing_00'):continue
    prefix=name[:-2]
    parts=[casings[prefix+f'{i:02d}'] for i in range(8)]
    axis=0 if 'Stern' in name else 1
    openings.append((axis,parts[6]['bounds_max'][axis],parts[2]['bounds_min'][axis],parts[0]['bounds_max'][2],parts[4]['bounds_min'][2]))
bm=bmesh.new();bm.from_mesh(reference.data)
bmesh.ops.transform(bm,matrix=matrices[reference.name],verts=list(bm.verts))
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.025)
bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True)
for iteration in range(5):
    edges=[e for e in bm.edges if e.calc_length()>.12 and min(v.co.y for v in e.verts)<-9.9 and min(v.co.z for v in e.verts)<10.95 and max(v.co.z for v in e.verts)>9.05]
    if not edges:break
    bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
bmesh.ops.triangulate(bm,faces=list(bm.faces))
bm.normal_update()
bm.verts.ensure_lookup_table()
bm.verts.index_update()
smooth_vertices=[v.co.copy() for v in bm.verts]
smooth_normals=[v.normal.copy() for v in bm.verts]
smooth_triangles=[tuple(v.index for v in f.verts) for f in bm.faces if abs(f.normal.z)<.88]
smooth_tree=BVHTree.FromPolygons(smooth_vertices,smooth_triangles,all_triangles=True)
source_faces=[]
bow_faces=[]
for face in bm.faces:
    center=face.calc_center_median()
    if max(v.co.y for v in face.verts)>15.99 and (abs(face.normal.z)<.88 or center.z<4.5 and abs(face.normal.z)<.995):
        bow_faces.append([(v.co.copy(),v.normal.copy()) for v in face.verts])
    if abs(face.normal.z)>.88 or center.z<4.5:continue
    source_faces.append([(v.co.copy(),v.normal.copy()) for v in face.verts])
bm.free()

def missing_side(point):
    if point.y>-9.98 or point.z<8.8 or point.z>11.62:return False
    return raw_tree.ray_cast(Vector((0,point.y,point.z)),Vector((1 if point.x>0 else -1,0,0)),20)[0] is None

def extend_side(point):
    hit=smooth_tree.find_nearest(point)
    tri=smooth_triangles[hit[2]]
    normal=barycentric_transform(hit[0],*[smooth_vertices[i] for i in tri],*[smooth_normals[i] for i in tri]).normalized()
    projected=point.copy()
    projected.x-=(point-hit[0]).dot(normal)/normal.x
    return projected,normal

for piece in pieces:
    if 'Quarterdeck_Sidewall' not in piece['intact']:continue
    obj=bpy.data.objects[piece['intact']]
    obj.data.calc_loop_triangles()
    ps=[matrices[obj.name]@v.co for v in obj.data.vertices]
    for tri in obj.data.loop_triangles:
        points=[ps[i] for i in tri.vertices]
        center=sum(points,Vector())/3
        normal=(points[1]-points[0]).cross(points[2]-points[0]).normalized()
        if normal.x*center.x>-.6*abs(center.x):continue
        polygon=[];previous=points[-1];before=missing_side(previous)
        for current in points:
            inside=missing_side(current)
            if inside!=before:
                low,high=0.,1.
                for iteration in range(18):
                    t=(low+high)*.5
                    if missing_side(previous.lerp(current,t))==before:low=t
                    else:high=t
                polygon.append(previous.lerp(current,(low+high)*.5))
            if inside:polygon.append(current)
            previous=current;before=inside
        for i in range(2,len(polygon)):
            extended=[extend_side(p) for p in [polygon[0],polygon[i-1],polygon[i]]]
            center=sum((p for p,n in extended),Vector())/3
            source_faces.append([(p+(p-center).normalized()*.025+n*.025,n) for p,n in extended])

def outward(point,normal):
    normal=normal.normalized()
    return normal if normal.dot(Vector((point.x,point.y,0)))>=0 else -normal

coverage_cache={}
def covered(point,normal):
    if point.z<4.9:return False
    if point.y<-9.98 and 9.10<point.z<11.62:
        for axis,lo,hi,bottom,top in openings:
            if axis==0 and point.y>-18.5 or axis==1 and abs(point.x)<4:continue
            c=point[axis]
            if lo<c<hi and bottom<point.z<top and min(c-lo,hi-c)+min(point.z-bottom,top-point.z)>.1:return False
        return True
    key=tuple(round(v,7) for v in point)
    if key in coverage_cache:return coverage_cache[key]
    normal=outward(point,normal)
    tangent=Vector((-normal.y,normal.x,0)).normalized()
    vertical=normal.cross(tangent).normalized()
    for offset in [Vector(),tangent*.018,-tangent*.018,vertical*.018,-vertical*.018]:
        hit=coverage_tree.ray_cast(point+offset-normal*.025,normal,.65)
        if hit[0] is not None:
            coverage_cache[key]=True
            return True
    coverage_cache[key]=False
    return False

cut_cache={}
def intersection(a,b):
    key=tuple(sorted((tuple(round(v,7) for v in a[0]),tuple(round(v,7) for v in b[0]))))
    if key in cut_cache:return cut_cache[key]
    low,high=0.,1.
    start=covered(*a)
    for iteration in range(13):
        t=(low+high)*.5
        p=a[0].lerp(b[0],t);n=a[1].lerp(b[1],t).normalized()
        if covered(p,n)==start:low=t
        else:high=t
    t=(low+high)*.5
    result=(a[0].lerp(b[0],t),a[1].lerp(b[1],t).normalized())
    cut_cache[key]=result
    return result

fragment_trees={}
def fragment_for(piece_index,point):
    if piece_index not in fragment_trees:
        vs=[];fs=[];names=[]
        for name in pieces[piece_index]['fragment_names']:
            obj=bpy.data.objects[name];obj.data.calc_loop_triangles()
            offset=len(vs);vs.extend(matrices[name]@v.co for v in obj.data.vertices)
            for triangle in obj.data.loop_triangles:fs.append(tuple(offset+i for i in triangle.vertices));names.append(name)
        fragment_trees[piece_index]=(BVHTree.FromPolygons(vs,fs,all_triangles=True),names)
    tree,names=fragment_trees[piece_index]
    hit=tree.find_nearest(point)
    return names[hit[2]] if hit[0] is not None else pieces[piece_index]['fragment_names'][0]

patches={}
def append_patch(piece_index,points,normals,positions=None):
    center=sum(points,Vector())/len(points)
    owner=pieces[piece_index]['intact']
    fragment=fragment_for(piece_index,center)
    if positions is None:positions=[p-outward(p,n)*.28 for p,n in zip(points,normals)]
    face_normal=(positions[1]-positions[0]).cross(positions[2]-positions[0])
    if face_normal.dot(-outward(center,sum(normals,Vector())))<0:
        positions.reverse();points=list(reversed(points));normals=list(reversed(normals))
    for name in [owner,fragment]:patches.setdefault(name,[]).append((positions,points))

def append_reveal(positions,normal):
    center=sum(positions,Vector())/len(positions)
    if (positions[1]-positions[0]).cross(positions[2]-positions[0]).dot(normal)<0:positions=list(reversed(positions))
    points=[p-normal*.28 for p in positions]
    hit=coverage_tree.find_nearest(center)
    index=owners[hit[2]]
    for name in [pieces[index]['intact'],fragment_for(index,center)]:patches.setdefault(name,[]).append((positions,points))

for triangle in source_faces:
    polygon=[];previous=triangle[-1];before=covered(*previous)
    for current in triangle:
        inside=covered(*current)
        if inside!=before:polygon.append(intersection(previous,current))
        if inside:polygon.append(current)
        previous=current;before=inside
    if len(polygon)<3:continue
    for index in range(2,len(polygon)):
        part=[polygon[0],polygon[index-1],polygon[index]]
        points=[v[0] for v in part];normals=[v[1] for v in part]
        if (points[1]-points[0]).cross(points[2]-points[0]).length<.000001:continue
        center=sum(points,Vector())/3
        hit=coverage_tree.find_nearest(center)
        append_patch(owners[hit[2]],points,normals)

def surface_at(axis,a,z,side):
    point=Vector((a,-19.6,z)) if axis==0 else Vector((side*5.5,a,z))
    hit=smooth_tree.find_nearest(point)
    tri=smooth_triangles[hit[2]]
    normal=outward(hit[0],barycentric_transform(hit[0],*[smooth_vertices[i] for i in tri],*[smooth_normals[i] for i in tri]))
    depth_axis=1 if axis==0 else 0
    if abs(normal[depth_axis])<.25:normal=Vector((0,-1,0)) if axis==0 else Vector((side,0,0))
    point[depth_axis]-=((point-hit[0]).dot(normal)+.28)/normal[depth_axis]
    return point+normal*.08,normal

def octagon(lo,hi,bottom,top,chamfer):
    return [(lo+chamfer,bottom),(hi-chamfer,bottom),(hi,bottom+chamfer),(hi,top-chamfer),(hi-chamfer,top),(lo+chamfer,top),(lo,top-chamfer),(lo,bottom+chamfer)]

for axis,lo,hi,bottom,top in set(openings):
    inner=octagon(lo,hi,bottom,top,.1)
    outer=octagon(lo-.3,hi+.3,bottom-.3,top+.3,.2)
    for side in ([-1] if axis==0 else [-1,1]):
        a=[surface_at(axis,c,z,side) for c,z in inner]
        b=[surface_at(axis,c,z,side) for c,z in outer]
        for i in range(8):
            j=(i+1)%8
            for triangle in [[a[i],b[i],b[j]],[a[i],b[j],a[j]]]:
                positions=[p for p,n in triangle];normals=[n for p,n in triangle]
                points=[p+n*.28 for p,n in triangle]
                hit=coverage_tree.find_nearest(sum(points,Vector())/3)
                append_patch(owners[hit[2]],points,normals,positions)
            depth=Vector((0,-.34,0)) if axis==0 else Vector((side*.34,0,0))
            positions=[a[i][0],a[j][0],a[j][0]+depth,a[i][0]+depth]
            center=sum(positions,Vector())/4
            target=surface_at(axis,(lo+hi)*.5,(bottom+top)*.5,side)[0]
            normal=(target-center)-depth.normalized()*(target-center).dot(depth.normalized())
            append_reveal(positions,normal.normalized())

surface=bmesh.new()
for piece in pieces:
    for positions,original in patches.get(piece['intact'],[]):
        try:surface.faces.new([surface.verts.new(p) for p in positions])
        except ValueError:pass
bmesh.ops.remove_doubles(surface,verts=list(surface.verts),dist=.015)
boundary_edges={e for e in surface.edges if e.is_boundary}
while boundary_edges:
    pending=[boundary_edges.pop()];component=[]
    while pending:
        edge=pending.pop();component.append(edge)
        for vertex in edge.verts:
            for linked in vertex.link_edges:
                if linked in boundary_edges:
                    boundary_edges.remove(linked);pending.append(linked)
    verts={v for e in component for v in e.verts}
    if len(verts)<3 or sum(e.calc_length() for e in component)>5:continue
    if any(sum(e.is_boundary for e in v.link_edges)!=2 for v in verts):continue
    if max(max(v.co[i] for v in verts)-min(v.co[i] for v in verts) for i in range(3))>1.6:continue
    center=sum((v.co for v in verts),Vector())/len(verts)
    if any(lo<center[axis]<hi and bottom<center.z<top and (center.y<-18.5 if axis==0 else abs(center.x)>4) for axis,lo,hi,bottom,top in openings):continue
    for face in bmesh.ops.holes_fill(surface,edges=component,sides=0)['faces']:
        ps=[v.co.copy() for v in face.verts]
        for i in range(2,len(ps)):
            positions=[ps[0],ps[i-1],ps[i]]
            center=sum(positions,Vector())/3
            hit=smooth_tree.find_nearest(center)
            tri=smooth_triangles[hit[2]]
            normal=outward(hit[0],barycentric_transform(hit[0],*[smooth_vertices[k] for k in tri],*[smooth_normals[k] for k in tri]))
            positions=[p+(p-center).normalized()*.015+normal*.08 for p in positions]
            points=[p+normal*.28 for p in positions]
            owner=coverage_tree.find_nearest(center)
            append_patch(owners[owner[2]],points,[normal]*3,positions)
surface.free()

def clip_axis(polygon,axis,value,above):
    result=[]
    if not polygon:return result
    previous=polygon[-1];before=(previous[0][axis]>=value)==above
    for current in polygon:
        inside=(current[0][axis]>=value)==above
        if inside!=before:
            t=(value-previous[0][axis])/(current[0][axis]-previous[0][axis])
            result.append((previous[0].lerp(current[0],t),previous[1].lerp(current[1],t).normalized()))
        if inside:result.append(current)
        previous=current;before=inside
    return result

outer_patches={}
for triangle in bow_faces:
    polygon=clip_axis(triangle,1,15.995,True)
    if len(polygon)<3:continue
    low=math.floor(min(p[0].z for p in polygon)/.43)
    high=math.floor(max(p[0].z for p in polygon)/.43)
    for row in range(low,high+1):
        band=clip_axis(clip_axis(polygon,2,row*.43,True),2,(row+1)*.43,False)
        for bottom,top in [(row*.43,row*.43+.012),(row*.43+.012,(row+1)*.43-.012),((row+1)*.43-.012,(row+1)*.43)]:
            strip=clip_axis(clip_axis(band,2,bottom,True),2,top,False)
            if len(strip)<3:continue
            for i in range(2,len(strip)):
                raw=[strip[0],strip[i-1],strip[i]]
                points=[p for p,n in raw]
                normals=[outward(p,n) for p,n in raw]
                positions=[p+n*(.125-.006*max(0,1-min(abs(p.z-row*.43),abs(p.z-(row+1)*.43))/.012)) for p,n in zip(points,normals)]
                cross=(positions[1]-positions[0]).cross(positions[2]-positions[0])
                if cross.length<1e-8:continue
                if cross.dot(sum(normals,Vector()))<0:positions.reverse();points.reverse();normals.reverse()
                center=sum(points,Vector())/3
                hit=coverage_tree.find_nearest(center)
                index=owners[hit[2]]
                for name in [pieces[index]['intact'],fragment_for(index,center)]:
                    outer_patches.setdefault(name,[]).append((positions,points,normals,row%4))

changed=set(manifest['appearance_objects'])
lining=[]
fragment_names={name for piece in pieces for name in piece['fragment_names']}
for piece in pieces:
    for name in [piece['intact']]+piece['fragment_names']:
        if len(bpy.data.objects[name].data.polygons):patches.setdefault(name,[])
changed.update(patches)
for name,parts in patches.items():
    obj=bpy.data.objects[name]
    mesh=bpy.data.meshes.new('V20_Interior_'+name)
    for material in obj.data.materials:mesh.materials.append(material)
    bm=bmesh.new()
    uv=bm.loops.layers.uv.verify()
    normal_xy=bm.loops.layers.uv.new('SurfaceNormalXY')
    normal_z=bm.loops.layers.uv.new('SurfaceNormalZ')
    inverse=matrices[name].inverted()
    interior=bpy.data.materials['V4_Hull_Ship_Art_Hull_Honey_Oak_V0']
    material=next((i for i,m in enumerate(mesh.materials) if m==interior),-1)
    if material<0:
        material=len(mesh.materials);mesh.materials.append(interior)
    fracture=next((i for i,m in enumerate(mesh.materials) if m and 'Fresh' in m.name),material)
    weld={}
    def add_face(positions,coords,mat,normals):
        area=sum(((positions[i]-positions[0]).cross(positions[i+1]-positions[0]) for i in range(1,len(positions)-1)),Vector())
        if area.length_squared<1e-16:return
        normals=[n.normalized() if n.length_squared>1e-12 else area.normalized() for n in normals]
        verts=[]
        for point in positions:
            key=tuple(round(v,7) for v in point)
            if key not in weld:weld[key]=bm.verts.new(inverse@point)
            verts.append(weld[key])
        if len(set(verts))<3:return
        try:face=bm.faces.new(verts)
        except ValueError:return
        face.material_index=mat;face.smooth=True
        for loop,coord,normal in zip(face.loops,coords,normals):
            loop[uv].uv=coord
            loop[normal_xy].uv=(normal.x,normal.y)
            loop[normal_z].uv=(normal.z,1)
    old=obj.data
    old.calc_loop_triangles()
    positions=[matrices[name]@v.co for v in old.vertices]
    def signed(point):
        hit=raw_tree.find_nearest(point)
        normal=outward(hit[0],hit[1])
        return (point-hit[0]).dot(normal)
    values=[signed(p) for p in positions]
    old_uv=old.uv_layers.active
    for triangle in old.loop_triangles:
        polygon=[(positions[index],old_uv.data[loop].uv.copy(),values[index]) for index,loop in zip(triangle.vertices,triangle.loops)]
        clipped=[];previous=polygon[-1]
        for current in polygon:
            if (current[2]>=-.02)!=(previous[2]>=-.02):
                t=(-.02-previous[2])/(current[2]-previous[2])
                clipped.append((previous[0].lerp(current[0],t),previous[1].lerp(current[1],t),-.02))
            if current[2]>=-.02:clipped.append(current)
            previous=current
        if clipped:
            trimmed=[];previous=clipped[-1]
            for current in clipped:
                if (current[0].y<=16)!=(previous[0].y<=16):
                    t=(16-previous[0].y)/(current[0].y-previous[0].y)
                    trimmed.append((previous[0].lerp(current[0],t),previous[1].lerp(current[1],t),0))
                if current[0].y<=16:trimmed.append(current)
                previous=current
            clipped=trimmed
        if len(clipped)<3:continue
        ps=[v[0] for v in clipped];coords=[v[1] for v in clipped]
        normal=(ps[1]-ps[0]).cross(ps[2]-ps[0]).normalized()
        center=sum(ps,Vector())/len(ps)
        expected=outward(center,raw_tree.find_nearest(center)[1])
        if abs(normal.dot(expected))>.6 and normal.dot(expected)<0:ps.reverse();coords.reverse();normal=-normal
        add_face(ps,coords,triangle.material_index,[normal]*len(ps))
        if center.y<-9.8 and center.z>8.7 and normal.dot(expected)>.8:
            back=[p-normal*.006 for p in reversed(ps)]
            transom=abs(center.y)>17 and abs(center.x)<4.3
            mapped=[((p.x if transom else p.y)*.28,p.z*.55) for p in back]
            add_face(back,mapped,material,[-expected]*len(back))
    outer_boundary={}
    for ps,raw,normals,variant in outer_patches.get(name,[]):
        wood=bpy.data.materials[f'V4_Hull_Ship_Art_Hull_Honey_Oak_V{variant}']
        slot=next((i for i,m in enumerate(mesh.materials) if m==wood),-1)
        if slot<0:slot=len(mesh.materials);mesh.materials.append(wood)
        coords=[(math.atan2(p.x/5.5,p.y/20)*5.5*.28,p.z*.55) for p in raw]
        add_face(ps,coords,slot,normals)
        for a,b,ra,rb in zip(ps,ps[1:]+ps[:1],raw,raw[1:]+raw[:1]):
            key=tuple(sorted((tuple(round(v,7) for v in a),tuple(round(v,7) for v in b))))
            outer_boundary.setdefault(key,[]).append((a,b,ra,rb))
    if name in fragment_names:
        for linked in outer_boundary.values():
            if len(linked)!=1:continue
            a,b,ra,rb=linked[0]
            ps=[b,a,ra,rb]
            normal=(ps[1]-ps[0]).cross(ps[2]-ps[0]).normalized()
            add_face(ps,[(0,0),((a-b).length*.5,0),((a-b).length*.5,.06),(0,.06)],fracture,[normal]*4)
    local_boundary={}
    for positions,original in parts:
        center=sum(original,Vector())/len(original)
        transom=abs(center.y)>17 and abs(center.x)<4.3
        coords=[((point.x if transom else point.y)*.28,point.z*.55) for point in original]
        normals=[(point-raw).normalized() for point,raw in zip(positions,original)]
        add_face(positions,coords,material,normals)
        for a,b,ra,rb in zip(positions,positions[1:]+positions[:1],original,original[1:]+original[:1]):
            key=tuple(sorted((tuple(round(v,7) for v in a),tuple(round(v,7) for v in b))))
            local_boundary.setdefault(key,[]).append((a,b,ra,rb))
    for linked in local_boundary.values():
        if name not in fragment_names:continue
        if len(linked)!=1:continue
        a,b,ra,rb=linked[0]
        ps=[b,a,ra,rb]
        normal=(ps[1]-ps[0]).cross(ps[2]-ps[0]).normalized()
        length=(a-b).length*.5
        add_face(ps,[(0,0),(length,0),(length,.14),(0,.14)],fracture if name in fragment_names else material,[normal]*4)
    bm.normal_update();bm.to_mesh(mesh);bm.free();mesh.validate(clean_customdata=True);mesh.update()
    child=bpy.data.objects.new('V20_Interior_'+name,mesh)
    obj.users_collection[0].objects.link(child)
    child.parent=obj
    child.matrix_parent_inverse=Matrix.Identity(4)
    child.matrix_basis=Matrix.Identity(4)
    if 'appearance_original_hide_render' not in obj:obj['appearance_original_hide_render']=obj.hide_render
    child.hide_render=obj['appearance_original_hide_render']
    obj.hide_render=True
    child['exclude_from_export']=True
    child['appearance_parent']=name
    lining.append({'name':child.name,'parent':name})

if not root.get('appearance_casing_depth_version'):
    for piece in casings.values():
        for name in [piece['intact']]+piece['fragment_names']:
            obj=bpy.data.objects[name]
            obj.data=obj.data.copy()
            inverse=matrices[name].inverted()
            for vertex in obj.data.vertices:
                p=matrices[name]@vertex.co
                hit=raw_tree.find_nearest(p)
                normal=outward(hit[0],hit[1])
                if (p-hit[0]).dot(normal)<-.02:p-=normal*.22
                vertex.co=inverse@p
            obj.data.update();changed.add(name)
    root['appearance_casing_depth_version']=1

manifest['appearance_objects']=sorted(changed)
manifest['appearance_lining']=lining
manifest['modified_objects']=sorted(set(manifest['modified_objects'])|{entry['name'] for entry in lining}|changed-set(manifest['new_objects']))
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,indent=2))
root['interior_lining_version']=1
result={'lining_objects':len(patches),'lining_triangles':sum(len(v) for v in patches.values())//2,'appearance_meshes':len(changed)}
