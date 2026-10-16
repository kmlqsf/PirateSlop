import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector

base=Path(__file__).resolve().parents[2]
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text())
document=json.loads((folder.parent/'ShipV3.json').read_text())
pieces={p['intact']:p for p in document['pieces']}
changed=set(manifest['appearance_objects'])
matrices={o.name:o.matrix_world.copy() for o in bpy.data.objects}
wood=bpy.data.materials['V4_Frame_Ship_Art_Frame_Warm_Timber']
carving=bpy.data.materials['V5_Ship_Art_Rail_Continuous_Carving']
profile=[(-.084,-.065),(.084,-.065),(.094,-.055),(.094,.055),(.084,.065),(-.084,.065),(-.094,.055),(-.094,-.055)]
perimeter=[0]
for a,b in zip(profile,profile[1:]+profile[:1]):perimeter.append(perimeter[-1]+(Vector(b)-Vector(a)).length)

def bezier(a,b,c,d,t):return a*(1-t)**3+b*(3*(1-t)**2*t)+c*(3*(1-t)*t*t)+d*t**3

for side,sign,straight in [('Port',-1,'V3_Bow_Stair_Handrail'),('Starboard',1,'V3_Bow_Stair_Handrail.001')]:
    lower=Vector((sign*4.03,8.57,9.455))
    upper=Vector((sign*4.03,10.54,10.356))
    direction=(upper-lower).normalized()
    bottom=Vector((sign*4.75,8.32,9.4))
    top=Vector((sign*4.39,10.91,10.4))
    curves=[lambda t:bezier(bottom,bottom-Vector((sign*.30,0,0)),lower-direction*.18,lower,t),lambda t:lower.lerp(upper,t),lambda t:bezier(upper,upper+direction*.18,top-Vector((sign*.16,0,0)),top,t)]
    names=[f'V3_Bow_{side}_Stair_RailReturn_00',straight,f'V3_Bow_{side}_Stair_RailReturn_01']
    cumulative=0
    for part,(name,curve) in enumerate(zip(names,curves)):
        steps=32 if part!=1 else 12
        samples=[curve(j/steps) for j in range(steps+1)]
        distances=[cumulative]
        for a,b in zip(samples,samples[1:]):distances.append(distances[-1]+(a-b).length)
        piece=pieces[name]
        def build(obj,lo,hi,fragment):
            count=max(2,round(steps*(hi-lo)))
            positions=[];faces=[];uvs=[];materials=[];ring_distances=[]
            for j in range(count+1):
                t=lo+(hi-lo)*j/count
                p=curve(t)
                forward=(curve(min(1,t+.0001))-curve(max(0,t-.0001))).normalized()
                if part==0 and t>.99999:forward=direction
                if part==2 and t<.00001:forward=direction
                right=Vector((forward.y,-forward.x,0)).normalized()
                up=right.cross(forward).normalized()
                distance=distances[-1]
                if t<1:
                    index=min(int(t*steps),steps-1)
                    distance=distances[index]+(p-samples[index]).length
                ring_distances.append(distance)
                for x,z in profile:positions.append(p+right*x+up*z)
            for j in range(count):
                for k in range(8):
                    faces.append((j*8+k,(j+1)*8+k,(j+1)*8+(k+1)%8,j*8+(k+1)%8))
                    side_face=k in (2,6)
                    v0,v1=(0,1) if side_face else (perimeter[k]*.6,perimeter[k+1]*.6)
                    uvs.append([(ring_distances[j]*.6,v0),(ring_distances[j]*.6,v1),(ring_distances[j+1]*.6,v1),(ring_distances[j+1]*.6,v0)])
                    materials.append(1 if side_face else 0)
            for end,enabled in [(0,fragment or part==0),(count,fragment or part==2)]:
                if not enabled:continue
                faces.append(tuple(end*8+k for k in range(8)) if end==0 else tuple(reversed([end*8+k for k in range(8)])))
                uvs.append([(x*.6,z*.6) for x,z in profile]);materials.append(2 if fragment else 0)
            mesh=bpy.data.meshes.new(obj.name+'_FittedRail')
            inverse=matrices[obj.name].inverted()
            mesh.from_pydata([inverse@p for p in positions],[],faces)
            fresh=next((m for m in obj.data.materials if m and 'Fresh' in m.name),wood)
            for material in [wood,carving,fresh]:mesh.materials.append(material)
            uv=mesh.uv_layers.new(name='UVMap')
            for face,coords,material in zip(mesh.polygons,uvs,materials):
                face.material_index=material
                for loop,coord in zip(face.loop_indices,coords):uv.data[loop].uv=coord
            bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
            mesh.update();obj.data=mesh;changed.add(obj.name)
        build(bpy.data.objects[name],0,1,False)
        for index,fragment in enumerate(piece['fragment_names']):build(bpy.data.objects[fragment],index/len(piece['fragment_names']),(index+1)/len(piece['fragment_names']),True)
        cumulative=distances[-1]

manifest['appearance_objects']=sorted(changed)
manifest['modified_objects']=sorted(set(manifest['modified_objects'])|changed-set(manifest['new_objects']))
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,indent=2))
bpy.data.objects['Ship_V3_Fitted_Root']['bow_handrail_fitting_version']=3
result={'appearance_meshes':len(changed),'handrail_fitting_version':3}
