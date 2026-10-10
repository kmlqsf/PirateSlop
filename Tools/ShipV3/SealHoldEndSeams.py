import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

base=Path('D:/projects/Pirate_BR/PirateGame')
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text(encoding='utf-8'))
document=json.loads((folder.parent/'ShipV3.json').read_text(encoding='utf-8'))
root=bpy.data.objects['Ship_V3_Fitted_Root']
assert not root.get('end_seams_version'),'Hull seams already sealed'
floor=manifest['floor_z'];ceiling=8.85
hawse=bpy.data.objects['V9_Anchor_Chain_Swing_Pivot'].matrix_world.translation
vertices=[];triangles=[]
for piece in document['pieces']:
 if piece['family']!='Hull':continue
 obj=bpy.data.objects[piece['intact']];obj.data.calc_loop_triangles()
 start=len(vertices)
 vertices.extend(obj.matrix_world@vertex.co for vertex in obj.data.vertices)
 triangles.extend(tuple(start+i for i in triangle.vertices) for triangle in obj.data.loop_triangles)
tree=BVHTree.FromPolygons(vertices,triangles,all_triangles=True)
def distance_to_segment(point,a,b):
 direction=b-a
 return (point-(a+direction*max(0,min(1,(point-a).dot(direction)/max(.000001,direction.length_squared))))).length
def inner_faces(obj,bm,basis=None):
 matrix=obj.matrix_world.to_3x3().inverted().transposed()
 mark=bm.faces.layers.int.get('HoldInnerSurface')
 marked=[face for face in bm.faces if mark and face[mark]==1]
 if marked:return marked
 if basis is None:
  candidates=[]
  for face in bm.faces:
   center=obj.matrix_world@face.calc_center_median()
   inward=Vector((-center.x,-center.y,0)).normalized()
   if (matrix@face.normal).normalized().dot(inward)>.55:candidates.append(face)
  if not candidates:return []
  basis=(matrix@max(candidates,key=lambda face:face.calc_area()).normal).normalized()
 return [face for face in bm.faces if (matrix@face.normal).normalized().dot(basis)>.75 and
  not (face.material_index<len(obj.data.materials) and obj.data.materials[face.material_index] and 'FreshWood' in obj.data.materials[face.material_index].name)]
def boundary(faces):
 selected=set(faces)
 return [(edge,next(face for face in edge.link_faces if face in selected)) for edge in {edge for face in faces for edge in face.edges}
  if sum(face in selected for face in edge.link_faces)==1]
modified=set(manifest['modified_objects']);changed=0;added=0;ends={'bow':0,'stern':0}
for piece in document['pieces']:
 if piece['family']!='Hull':continue
 intact=bpy.data.objects[piece['intact']]
 points=[intact.matrix_world@vertex.co for vertex in intact.data.vertices]
 if not points or max(p.z for p in points)<floor-.02 or min(p.z for p in points)>ceiling:continue
 source=bmesh.new();source.from_mesh(intact.data)
 faces=inner_faces(intact,source)
 if not faces:source.free();continue
 normal_matrix=intact.matrix_world.to_3x3().inverted().transposed()
 basis=sum(((normal_matrix@face.normal).normalized()*face.calc_area() for face in faces),Vector()).normalized()
 segments=[tuple(intact.matrix_world@vertex.co for vertex in edge.verts) for edge,_ in boundary(faces)]
 source.free()
 for name in [piece['intact']]+piece['fragment_names']:
  obj=bpy.data.objects[name]
  if not len(obj.data.polygons):continue
  if obj.data.users>1:obj.data=obj.data.copy()
  bm=bmesh.new();bm.from_mesh(obj.data)
  selected=inner_faces(obj,bm,basis)
  edges=[]
  offsets={};normals={}
  normal_matrix=obj.matrix_world.to_3x3().inverted().transposed()
  for edge,face in boundary(selected):
   a,b=[obj.matrix_world@vertex.co for vertex in edge.verts]
   middle=(a+b)*.5
   if middle.z<floor-.05 or middle.z>ceiling or (middle-hawse).length<.45:continue
   if name!=piece['intact'] and min((distance_to_segment(middle,*segment) for segment in segments),default=100)>.035:continue
   normal=(normal_matrix@face.normal).normalized()
   tangent=(b-a).normalized()
   outward=middle-obj.matrix_world@face.calc_center_median()
   outward-=normal*outward.dot(normal)+tangent*outward.dot(tangent)
   if outward.length<.00001:continue
   outward.normalize()
   edges.append((edge,face))
   for vertex in edge.verts:
    offsets.setdefault(vertex,[]).append(outward)
    normals[vertex]=normals.get(vertex,Vector())+normal
  duplicates={};inverse=obj.matrix_world.inverted()
  for vertex,directions in offsets.items():
   point=obj.matrix_world@vertex.co
   if (point-hawse).length<.38:continue
   outward=sum(directions,Vector()).normalized()
   width=.05/max(.5,min(outward.dot(direction) for direction in directions))
   q=point+outward*width-normals[vertex].normalized()*.012
   radius=math.hypot(q.x,q.y)
   if radius>.01:
    direction=Vector((q.x/radius,q.y/radius,0))
    hit=tree.ray_cast(Vector((direction.x*35,direction.y*35,q.z)),-direction,35)
    if hit[0]:
     limit=math.hypot(hit[0].x,hit[0].y)-.018
     if radius>limit:q.x*=limit/radius;q.y*=limit/radius
   new=bm.verts.new(inverse@q)
   duplicates[vertex]=new
  uv_layers=list(bm.loops.layers.uv.values())
  total=0
  for edge,face in edges:
   loop=next(loop for loop in face.loops if loop.edge==edge)
   a=loop.vert;b=loop.link_loop_next.vert
   if a not in duplicates or b not in duplicates:continue
   lip=bm.faces.new((b,a,duplicates[a],duplicates[b]))
   lip.material_index=face.material_index;lip.smooth=face.smooth
   for layer in uv_layers:
    auv=loop[layer].uv.copy();buv=loop.link_loop_next[layer].uv.copy()
    middle=(auv+buv)*.5
    center=sum((entry[layer].uv for entry in face.loops),Vector((0,0)))/len(face.loops)
    tangent=buv-auv
    outward=middle-center
    if tangent.length_squared>.000001:outward-=tangent*outward.dot(tangent)/tangent.length_squared
    scale=(auv-buv).length/max(.001,(obj.matrix_world.to_3x3()@(a.co-b.co)).length)
    if outward.length>.00001:outward.normalize()
    for entry,uv in zip(lip.loops,(buv,auv,auv+outward*.05*scale,buv+outward*.05*scale)):entry[layer].uv=uv
   total+=1
  if total:
   bm.normal_update();bm.to_mesh(obj.data);obj.data.update()
   modified.add(name);changed+=1;added+=total
   ends['bow' if piece['center'][1]>0 else 'stern']+=1
  bm.free()
manifest['modified_objects']=sorted(modified)
manifest['version']=11;manifest['end_seams_version']=2;manifest['pump_down_angle']=125;manifest['stern_surface_version']=2
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
root['bilge_deck_version']=11;root['end_seams_version']=2;root['stern_surface_version']=2;root['interior_seams_version']=2
bpy.context.view_layer.update()
result={'version':11,'meshes':changed,'edge_faces':added,'ends':ends,'preserved_original_vertices':True}
