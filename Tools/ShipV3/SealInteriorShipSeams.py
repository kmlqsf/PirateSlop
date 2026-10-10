import bpy,json
from pathlib import Path
from mathutils import Vector
base=Path('D:/projects/Pirate_BR/PirateGame')
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text(encoding='utf-8'))
document=json.loads((folder.parent/'ShipV3.json').read_text(encoding='utf-8'))
root=bpy.data.objects['Ship_V3_Fitted_Root']
assert not root.get('interior_seams_version'), 'Interior seams already sealed'
modified=set(manifest['modified_objects'])
changed=0;pieces_changed=0
for piece in document['pieces']:
 if piece['family']!='Deck':continue
 intact=bpy.data.objects[piece['intact']]
 if not len(intact.data.polygons):continue
 positions=[intact.matrix_world@v.co for v in intact.data.vertices]
 center=sum(positions,Vector())/len(positions)
 deck=piece['family']=='Deck'
 toward=Vector((0,0,-1)) if deck else Vector((-center.x,-center.y*.04,0)).normalized()
 normal_matrix=intact.matrix_world.to_3x3().inverted().transposed()
 faces=[p for p in intact.data.polygons if (normal_matrix@p.normal).normalized().dot(toward)>.65]
 if not faces:continue
 selected=set(i for p in faces for i in p.vertices)
 axes=(0,1) if deck else (1,2)
 ranges=[(min(positions[i][axis] for i in selected),max(positions[i][axis] for i in selected)) for axis in axes]
 pads=(.018,.018) if deck else (.035,.07)
 n=sum(((normal_matrix@p.normal).normalized()*p.area for p in faces),Vector()).normalized()
 dominant=2 if deck else 0
 if abs(n[dominant])<.5:continue
 pieces_changed+=1
 for name in [piece['intact']]+piece['fragment_names']:
  obj=bpy.data.objects[name]
  if not len(obj.data.polygons):continue
  if obj.data.users>1:obj.data=obj.data.copy()
  nm=obj.matrix_world.to_3x3().inverted().transposed()
  indices=set(i for p in obj.data.polygons if (nm@p.normal).normalized().dot(toward)>.6 for i in p.vertices)
  inverse=obj.matrix_world.inverted()
  for index in indices:
   point=obj.matrix_world@obj.data.vertices[index].co
   delta=Vector()
   for axis,(low,high),pad in zip(axes,ranges,pads):
    half=(high-low)*.5
    if half>.002:delta[axis]=(point[axis]-(low+high)*.5)*pad/half
   if deck:
    if -.81<=point.x<=.81 and 3.39<=point.y<=10.06:
     if abs(abs(point.x)-.75)<.045:delta.x=0
     if abs(point.y-3.45)<.045 or abs(point.y-10)<.045:delta.y=0
    if (point.x-1.45)**2+(point.y-15.7)**2<.36**2:delta.x=delta.y=0
   elif abs(point.z-manifest['floor_z'])<.001:delta.z=0
   delta[dominant]=-sum(n[axis]*delta[axis] for axis in axes)/n[dominant]
   obj.data.vertices[index].co=inverse@(point+delta)
  obj.data.update()
  modified.add(name);changed+=1
manifest['modified_objects']=sorted(modified)
manifest['version']=8
manifest['interior_seams_version']=1
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
root['bilge_deck_version']=8
root['interior_seams_version']=1
bpy.context.view_layer.update()
result={'pieces':pieces_changed,'meshes':changed,'modified':len(modified),'version':8}
