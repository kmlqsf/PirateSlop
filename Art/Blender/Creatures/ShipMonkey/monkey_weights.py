import bpy, math
from mathutils import Vector

mesh=bpy.data.objects['ShipMonkeyMesh']
rig=bpy.data.objects['ShipMonkeyRig']
mesh.vertex_groups.clear()
groups={b.name:mesh.vertex_groups.new(name=b.name) for b in rig.data.bones if b.use_deform}
image=next(n.image for n in mesh.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE')
import numpy as np
pixels=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(pixels)
pixels=pixels.reshape((image.size[1],image.size[0],4))
colors=np.zeros((len(mesh.data.vertices),3));counts=np.zeros(len(mesh.data.vertices))
uv=mesh.data.uv_layers.active.data
for loop in mesh.data.loops:
    coord=uv[loop.index].uv
    px=int(coord.x*(image.size[0]-1))%image.size[0];py=int(coord.y*(image.size[1]-1))%image.size[1]
    colors[loop.vertex_index]+=pixels[py,px,:3];counts[loop.vertex_index]+=1
colors/=np.maximum(counts[:,None],1)
def segment_distance(p,bone):
    a=bone.head_local;v=bone.tail_local-a
    t=max(0,min(1,(p-a).dot(v)/v.length_squared))
    return (p-a-v*t).length
def blend(a,b,t):
    t=max(0,min(1,t));t=t*t*(3-2*t)
    return {a:1-t,b:t}
def closest(p,names,sigma=.027):
    items=sorted((segment_distance(p,rig.data.bones[n]),n) for n in names)[:2]
    smallest=items[0][0]
    return {n:math.exp(-((d-smallest)/sigma)**2) for d,n in items}
for vertex in mesh.data.vertices:
    p=vertex.co;x,y,z=p
    ax=abs(x);side='L' if x>0 else 'R'
    tail_names=['Tail%02d'%i for i in range(10)]
    tail_distance=min(segment_distance(p,rig.data.bones[n]) for n in tail_names)
    if z<.373 and tail_distance<.041 and ((x>.095 and y>-.025) or (x>.066 and z>.2 and y>-.029)):
        weights=closest(p,tail_names,.018)
        if x<.108 and z>.2:
            factor=smooth_factor=max(0,min(1,(x-.071)/.037))
            weights={n:w*factor for n,w in weights.items()};weights['Pelvis']=1-factor
    elif z>=.552:
        weights={'Head':1}
        if y>.073 and x>.025 and .56<z<.705:
            weights=blend('Head','Bandana',max(0,(y-.084)/.046)*max(0,(.688-z)/.1))
    elif z>.521 and ax<.17:
        weights=blend('Neck','Head',(z-.521)/.031)
    elif ax>.12 and z>.448:
        if ax<.16:
            weights=blend('Chest','UpperArm.'+side,(ax-.105)/.055)
        elif ax<.28:
            weights=blend('UpperArm.'+side,'Forearm.'+side,(ax-.207)/.056)
        elif ax<.397:
            weights=blend('Forearm.'+side,'Hand.'+side,(ax-.347)/.045)
        elif z<.467 and y<-.014:
            weights=blend('Hand.'+side,'Thumb.'+side,(ax-.384)/.034)
        else:
            weights=blend('Hand.'+side,'Fingers.'+side,(ax-.412)/.027)
    elif z>.285:
        if z<.385: weights=blend('Pelvis','Spine',(z-.322)/.057)
        elif z<.485: weights=blend('Spine','Chest',(z-.405)/.05)
        else: weights=blend('Chest','Neck',(z-.493)/.035)
    elif .025<x<.16 and y<-.037 and .125<z<.306 and colors[vertex.index][0]>colors[vertex.index][1]*1.72 and colors[vertex.index][0]>.25:
        weights=blend('Pelvis','Sash',(.279-z)/.084)
    elif y<-.060 and z>.235:
        weights={'Pelvis':1}
    elif z>.245:
        weights=blend('Thigh.'+side,'Pelvis',(z-.245)/.05)
    elif z>.11:
        weights=blend('Shin.'+side,'Thigh.'+side,(z-.14)/.055)
    elif z>.051:
        weights=blend('Foot.'+side,'Shin.'+side,(z-.060)/.043)
    else:
        weights=blend('Foot.'+side,'Toes.'+side,(-y-.063)/.047)
    total=sum(weights.values())
    for name,weight in weights.items():
        if weight>0: groups[name].add([vertex.index],weight/total,'REPLACE')
names=list(groups)
weights=np.zeros((len(mesh.data.vertices),len(names)),dtype=np.float32)
for vertex in mesh.data.vertices:
    for g in vertex.groups: weights[vertex.index,g.group]=g.weight
edge_indices=np.empty(len(mesh.data.edges)*2,dtype=np.int32);mesh.data.edges.foreach_get('vertices',edge_indices)
edge_indices=edge_indices.reshape((-1,2));left=edge_indices[:,0];right=edge_indices[:,1]
degree=np.bincount(np.concatenate((left,right)),minlength=len(weights)).astype(np.float32)
for iteration in range(8):
    accumulated=np.zeros_like(weights)
    np.add.at(accumulated,left,weights[right]);np.add.at(accumulated,right,weights[left])
    weights=.55*weights+.45*accumulated/np.maximum(degree[:,None],1)
for group in groups.values(): group.remove(list(range(len(mesh.data.vertices))))
for index,row in enumerate(weights):
    selected=np.argsort(row)[-4:]
    selected=[i for i in selected if row[i]>.002]
    total=sum(row[i] for i in selected)
    for i in selected: groups[names[i]].add([index],float(row[i]/total),'REPLACE')
result={'unweighted':sum(1 for v in mesh.data.vertices if not v.groups),'max_influences':max(len(v.groups) for v in mesh.data.vertices)}
