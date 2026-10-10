import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
base=Path('D:/projects/Pirate_BR/PirateGame')
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
doc=json.loads((folder.parent/'ShipV3.json').read_text(encoding='utf-8'))
manifest=json.loads((folder/'BilgeDeck.json').read_text(encoding='utf-8'))
root=bpy.data.objects['Ship_V3_Fitted_Root']
assert not root.get('stern_surface_version')
vertices=[];faces=[]
for p in doc['pieces']:
 if p['family']!='Hull':continue
 o=bpy.data.objects[p['intact']];o.data.calc_loop_triangles()
 at=len(vertices);vertices.extend(o.matrix_world@v.co for v in o.data.vertices)
 faces.extend(tuple(at+i for i in t.vertices) for t in o.data.loop_triangles)
tree=BVHTree.FromPolygons(vertices,faces,all_triangles=True)
angles=721;rows=91;bottom=manifest['floor_z']-.08;top=8.85
angle_min=-2.45;angle_max=-.69
profile=[]
for j in range(rows):
 z=bottom+(top-bottom)*j/(rows-1)
 row=[]
 for i in range(angles):
  a=angle_min+(angle_max-angle_min)*i/(angles-1)
  direction=Vector((math.cos(a),math.sin(a),0))
  hit=tree.ray_cast(Vector((0,0,z)),direction,30)
  row.append(hit[3] if hit[0] else 0)
 profile.append(row)
for iteration in range(3):
 smooth=[]
 for j,row in enumerate(profile):
  output=[]
  for i,radius in enumerate(row):
   values=[profile[y][x] for y in range(max(0,j-1),min(rows,j+2)) for x in range(max(0,i-2),min(angles,i+3)) if profile[y][x]>0]
   values.sort()
   median=values[len(values)//2] if values else radius
   output.append(median if radius<=0 else radius*.35+median*.65)
  smooth.append(output)
 profile=smooth
def sample(a,z):
 u=max(0,min(angles-1.0001,(a-angle_min)/(angle_max-angle_min)*(angles-1)))
 v=max(0,min(rows-1.0001,(z-bottom)/(top-bottom)*(rows-1)))
 i=int(u);j=int(v);u-=i;v-=j
 return (profile[j][i]*(1-u)+profile[j][i+1]*u)*(1-v)+(profile[j+1][i]*(1-u)+profile[j+1][i+1]*u)*v
modified=set(manifest['modified_objects']);changed=0
for p in doc['pieces']:
 if p['family']!='Hull' or p['center'][1]>=-12:continue
 intact=bpy.data.objects[p['intact']]
 ps=[intact.matrix_world@v.co for v in intact.data.vertices]
 if not ps:continue
 center=sum(ps,Vector())/len(ps)
 if center.z<manifest['floor_z']-.15 or center.z>8.85:continue
 selected=[]
 nm=intact.matrix_world.to_3x3().inverted().transposed()
 candidates=[]
 for face in intact.data.polygons:
  c=intact.matrix_world@face.center
  if (nm@face.normal).normalized().dot(Vector((-c.x,-c.y,0)).normalized())>.35:candidates.append(face)
 if not candidates:continue
 main=max(candidates,key=lambda face:face.area)
 inside_normal=(nm@main.normal).normalized()
 for face in intact.data.polygons:
  if (nm@face.normal).normalized().dot(inside_normal)>.85:selected.extend(ps[i] for i in face.vertices)
 if not selected:continue
 ar=[math.atan2(v.y,v.x) for v in selected];zs=[v.z for v in selected]
 ac=(min(ar)+max(ar))*.5;ah=max(.0001,(max(ar)-min(ar))*.5)
 zc=(min(zs)+max(zs))*.5;zh=max(.001,(max(zs)-min(zs))*.5)
 angular_pad=.018/max(1,math.hypot(center.x,center.y))
 for name in [p['intact']]+p['fragment_names']:
  o=bpy.data.objects[name]
  if not len(o.data.polygons):continue
  if o.data.users>1:o.data=o.data.copy()
  bm=bmesh.new();bm.from_mesh(o.data)
  normal_matrix=o.matrix_world.to_3x3().inverted().transposed()
  mark=bm.faces.layers.int.new('HoldInnerSurface')
  inside=[]
  for face in bm.faces:
   c=o.matrix_world@face.calc_center_median()
   material=o.data.materials[face.material_index] if face.material_index<len(o.data.materials) else None
   if material and 'FreshWood' in material.name:continue
   if (normal_matrix@face.normal).normalized().dot(inside_normal)>.85:inside.append(face);face[mark]=1
  edges=set(e for face in inside for e in face.edges)
  if edges:
   longest=max((o.matrix_world.to_3x3()@(e.verts[0].co-e.verts[1].co)).length for e in edges)
   cuts=min(18,max(0,math.ceil(longest/.24)-1))
   if cuts:bmesh.ops.subdivide_edges(bm,edges=list(edges),cuts=cuts,use_grid_fill=True)
  points=set()
  for face in bm.faces:
   if face[mark]==1:points.update(face.verts)
  inverse=o.matrix_world.inverted()
  for vertex in points:
   q=o.matrix_world@vertex.co
   if q.z<manifest['floor_z']-.09 or q.z>8.85:continue
   a=math.atan2(q.y,q.x)
   if a<angle_min+.01 or a>angle_max-.01:continue
   a+=(a-ac)*angular_pad/ah
   if abs(q.z-manifest['floor_z'])>.001:q.z+=(q.z-zc)*.014/zh
   edge=max(abs((a-ac)/(ah+angular_pad)),abs((q.z-zc)/(zh+.014)))
   radius=sample(a,q.z)-.018+.012*max(0,min(1,(edge-.86)/.14))
   if radius<=0:continue
   q.x=math.cos(a)*radius;q.y=math.sin(a)*radius
   vertex.co=inverse@q
  bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
  bm.to_mesh(o.data);bm.free();o.data.update()
  modified.add(name);changed+=1
manifest['modified_objects']=sorted(modified);manifest['version']=9;manifest['stern_surface_version']=1
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
root['bilge_deck_version']=9;root['stern_surface_version']=1
bpy.context.view_layer.update()
result={'stern_meshes':changed,'version':9}
