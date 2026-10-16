import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

base=Path(__file__).resolve().parents[2]
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text())
changed=set(manifest['appearance_objects'])
matrices={o.name:o.matrix_world.copy() for o in bpy.data.objects}
for side,sign in [('Port',-1),('Starboard',1)]:
    obj=bpy.data.objects['V3_Transfer_Boarding_Net_'+side]
    matrix=matrices[obj.name];inverse=matrix.inverted()
    body_name='V3_Boarding_Net_'+side+'_AttachmentBody'
    stored_body=bpy.data.meshes.get(body_name)
    mesh=(stored_body if stored_body else obj.data).copy();mesh.transform(matrix)
    bm=bmesh.new();bm.from_mesh(mesh)
    cut=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=(0,0,8.6),plane_no=(0,0,1),clear_outer=True,dist=.00001)
    edges=[e for e in cut['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and e.is_valid and e.is_boundary]
    if edges:bmesh.ops.holes_fill(bm,edges=edges,sides=0)
    if stored_body is None:
        backup=mesh.copy();backup.name=body_name
        bm.to_mesh(backup);backup.transform(inverse);backup.use_fake_user=True
    body=[v.co.copy() for v in bm.verts]
    rope=bpy.data.materials['Ship_Hemp_Rope_PBR.001']
    if rope not in mesh.materials[:]:mesh.materials.append(rope)
    material=list(mesh.materials).index(rope)
    uv=bm.loops.layers.uv.verify()
    def tube(path,radius=.012):
        distances=[0]
        for a,b in zip(path,path[1:]):distances.append(distances[-1]+(a-b).length)
        for strand in range(3):
            rings=[]
            for j,p in enumerate(path):
                forward=(path[min(j+1,len(path)-1)]-path[max(0,j-1)]).normalized()
                right=forward.cross(Vector((0,0,1))).normalized()
                if right.length<.1:right=Vector((1,0,0))
                up=right.cross(forward).normalized()
                angle=distances[j]/.16*math.tau+strand*math.tau/3
                center=p+(right*math.cos(angle)+up*math.sin(angle))*.016
                rings.append([bm.verts.new(center+(right*math.cos(k*math.tau/8)+up*math.sin(k*math.tau/8))*radius) for k in range(8)])
            for j in range(len(rings)-1):
                for k in range(8):
                    face=bm.faces.new((rings[j][k],rings[j][(k+1)%8],rings[j+1][(k+1)%8],rings[j+1][k]))
                    face.material_index=material;face.smooth=True
                    for loop,coord in zip(face.loops,[(distances[j]*3,k/8),(distances[j]*3,(k+1)/8),(distances[j+1]*3,(k+1)/8),(distances[j+1]*3,k/8)]):loop[uv].uv=coord
    for y in [-.9648,-2.9649]:
        fore=y>-2
        bollard_id=(8 if fore else 6) if sign<0 else (9 if fore else 7)
        bollard=bpy.data.objects[f'V3_Transfer_S000_Bollards_P{bollard_id:04d}_Intact']
        points=[matrices[bollard.name]@v.co for v in bollard.data.vertices]
        cap=[p for p in points if p.z>9.27]
        center=sum(cap,Vector())/len(cap)
        center.z=9.175
        wraps=[center+Vector((sign*.127*math.cos(j*math.tau/48),.12*math.sin(j*math.tau/48),-.04*j/48)) for j in range(97)]
        tube(wraps)
        candidates=[p for p in body if 8.35<p.z<8.54 and abs(p.y-y)<.5]
        edge=(max if fore else min)(p.y for p in candidates)
        neck=[p for p in candidates if abs(p.y-edge)<.065]
        node=sum(neck,Vector())/len(neck)
        node.z=8.45
        knot=[node+Vector((sign*.055*math.cos(j*math.tau/40),.055*math.sin(j*math.tau/40),.06-.035*j/40)) for j in range(81)]
        start=wraps[-1];target=knot[0]
        a=start+Vector((sign*.22,0,.015))
        b=target+Vector((-sign*.25,0,.36))
        tail=[start*(1-t)**3+a*(3*(1-t)**2*t)+b*(3*(1-t)*t*t)+target*t**3 for t in [j/40 for j in range(41)]]
        tube(tail+knot[1:])
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh);bm.free();mesh.transform(inverse);mesh.update();obj.data=mesh
    obj.vertex_groups.clear()
    groups={name:obj.vertex_groups.new(name=name) for name in ['Attach_Ear_Fore','Attach_Ear_Aft','Flex_Net','Net_Pin_Ears','Net_Deform_Body']}
    assignments={name:[] for name in groups}
    for vertex in mesh.vertices:
        p=matrix@vertex.co
        if p.z>8.3:
            assignments['Attach_Ear_Fore' if p.y>-2 else 'Attach_Ear_Aft'].append(vertex.index)
            assignments['Net_Pin_Ears'].append(vertex.index)
        else:
            assignments['Flex_Net'].append(vertex.index)
            assignments['Net_Deform_Body'].append(vertex.index)
    for name,indices in assignments.items():
        if indices:groups[name].add(indices,1,'REPLACE')
    changed.add(obj.name)
manifest['appearance_objects']=sorted(changed)
manifest['modified_objects']=sorted(set(manifest['modified_objects'])|changed-set(manifest['new_objects']))
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,indent=2))
bpy.data.objects['Ship_V3_Fitted_Root']['boarding_attachment_fitting_version']=3
result={'net_attachment_version':3,'nets':2}
