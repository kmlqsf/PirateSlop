import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

base=Path(__file__).resolve().parents[2]
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text(encoding='utf-8'))
document=json.loads((folder.parent/'ShipV3.json').read_text(encoding='utf-8'))
root=bpy.data.objects['Ship_V3_Fitted_Root']
assert not root.get('appearance_repair_version'), 'Appearance repair already applied'
changed=set()
pieces={p['intact']:p for p in document['pieces']}
matrices={o.name:o.matrix_world.copy() for o in bpy.data.objects}

def world_points(obj):
    return [matrices[obj.name]@v.co for v in obj.data.vertices]

def bounds(points):
    return [Vector([fn(p[i] for p in points) for i in range(3)]) for fn in (min,max)]

def edit(obj):
    if obj.data.users>1:obj.data=obj.data.copy()
    changed.add(obj.name)
    return matrices[obj.name].inverted()

def variants(piece):
    return [bpy.data.objects[n] for n in [piece['intact']]+piece['fragment_names']]

for piece in document['pieces']:
    if not any(k in piece['intact'] for k in ('Quarterdeck_Sidewall','V6_Hold_Bulkhead_Board')):continue
    intact=bpy.data.objects[piece['intact']]
    points=world_points(intact)
    if not points:continue
    lo,hi=bounds(points)
    side='Sidewall' in intact.name
    axis=1 if side else 0
    for obj in variants(piece):
        inverse=edit(obj)
        for vertex in obj.data.vertices:
            p=matrices[obj.name]@vertex.co
            p[axis]+=(p[axis]-(lo[axis]+hi[axis])*.5)*.006/max(.01,(hi[axis]-lo[axis])*.5)
            p.z+=(p.z-(lo.z+hi.z)*.5)*.006/max(.01,(hi.z-lo.z)*.5)
            vertex.co=inverse@p
        obj.data.update()

for name in ['V3_Transfer_S029_Helm_P0553_Intact','V3_Transfer_S053_MizzenMast_P0992_Intact']:
    piece=pieces[name]
    for obj in variants(piece):
        inverse=edit(obj)
        for vertex in obj.data.vertices:
            p=matrices[obj.name]@vertex.co
            p.z=max(p.z,11.605)
            vertex.co=inverse@p
        obj.data.update()

for side in ['Port','Starboard']:
    net=bpy.data.objects['V3_Transfer_Boarding_Net_'+side]
    inverse=edit(net)
    for group_name,bollard_id in [('Attach_Ear_Aft',6 if side=='Port' else 7),('Attach_Ear_Fore',8 if side=='Port' else 9)]:
        group=net.vertex_groups[group_name]
        vertices=[v for v in net.data.vertices if any(g.group==group.index and g.weight>.5 for g in v.groups)]
        points=[matrices[net.name]@v.co for v in vertices]
        lo,hi=bounds(points)
        center=(lo+hi)*.5
        bollard=bpy.data.objects[f'V3_Transfer_S000_Bollards_P{bollard_id:04d}_Intact']
        bp=world_points(bollard)
        top=max(p.z for p in bp)
        cap=[p for p in bp if p.z>top-.08]
        target=sum(cap,Vector())/len(cap)
        for vertex,p in zip(vertices,points):
            p.x=target.x+(p.x-center.x)*.58
            p.y=target.y+(p.y-center.y)*.68
            p.z=9.115+(p.z-center.z)*.85
            vertex.co=inverse@p
    net.data.update()

for piece in document['pieces']:
    if 'Bow_' not in piece['intact'] or 'Stair_RailReturn' not in piece['intact']:continue
    for obj in variants(piece):
        edit(obj)
        for slot,material in enumerate(obj.data.materials):
            if material and 'Rail_Continuous_Carving' in material.name:
                obj.data.materials[slot]=bpy.data.materials['V4_Frame_Ship_Art_Frame_Warm_Timber']
        points=world_points(obj)
        lo,hi=bounds(points)
        direction=Vector((hi.x-lo.x,hi.y-lo.y,0)).normalized()
        if obj.name==piece['intact']:
            center=(lo+hi)*.5
            inverse=matrices[obj.name].inverted()
            for vertex,p in zip(obj.data.vertices,points):
                p+=direction*math.copysign(.035,(p-center).dot(direction))
                vertex.co=inverse@p
        uv=obj.data.uv_layers.active
        for polygon in obj.data.polygons:
            for index in polygon.loop_indices:
                p=matrices[obj.name]@obj.data.vertices[obj.data.loops[index].vertex_index].co
                uv.data[index].uv=(p.x*.55,p.y*.55 if abs(polygon.normal.z)>.5 else p.z*.55)
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        bmesh.ops.bevel(bm,geom=list(bm.edges),offset=.018,segments=3,affect='EDGES',clamp_overlap=True)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()

for name in manifest['new_objects']:
    if not name.startswith('V19_BilgeStair_Stringer_'):continue
    obj=bpy.data.objects[name]
    inverse=edit(obj)
    points=world_points(obj)
    center=(min(p.x for p in points)+max(p.x for p in points))*.5
    shift=math.copysign(.07,center)
    for vertex in obj.data.vertices:
        p=matrices[obj.name]@vertex.co
        p.x=center+shift+(p.x-center)*1.72
        vertex.co=inverse@p
    obj.data.update()

manifest['appearance_objects']=sorted(changed)
manifest['appearance_repair_version']=1
manifest['modified_objects']=sorted(set(manifest['modified_objects'])|changed-set(manifest['new_objects']))
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
root['appearance_repair_version']=1
bpy.context.view_layer.update()
refine_script=base/'Tools/ShipV3/RefineShipAppearance.py'
refine_namespace=dict(globals(),__file__=str(refine_script))
exec(compile(refine_script.read_text(),str(refine_script),'exec'),refine_namespace)
result=refine_namespace['result']
