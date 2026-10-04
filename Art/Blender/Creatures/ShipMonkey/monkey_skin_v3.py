import bpy, math
import numpy as np

mesh = bpy.data.objects['ShipMonkeyMesh']
def blend(a, b, t):
    t = max(0, min(1, t)); t = t*t*(3-2*t)
    return {a: 1-t, b: t}
changed = []
for vertex in mesh.data.vertices:
    x, y, z = vertex.co; ax = abs(x); side = 'L' if x > 0 else 'R'
    if ax <= .118 or not .405 < z < .552:
        continue
    if ax < .188:
        weights = blend('Chest', 'UpperArm.'+side, (ax-.102)/.046)
    elif ax < .28:
        weights = blend('UpperArm.'+side, 'Forearm.'+side, (ax-.207)/.056)
    else:
        continue
    for entry in list(vertex.groups):
        mesh.vertex_groups[entry.group].remove([vertex.index])
    for name, weight in weights.items():
        if weight > 0: mesh.vertex_groups[name].add([vertex.index], weight, 'REPLACE')
    changed.append(vertex.index)
names=[g.name for g in mesh.vertex_groups]
weights=np.zeros((len(mesh.data.vertices),len(names)),dtype=np.float32)
for vertex in mesh.data.vertices:
    for entry in vertex.groups:weights[vertex.index,entry.group]=entry.weight
edges=np.empty(len(mesh.data.edges)*2,dtype=np.int32);mesh.data.edges.foreach_get('vertices',edges)
edges=edges.reshape((-1,2));left,right=edges[:,0],edges[:,1]
degree=np.bincount(np.concatenate((left,right)),minlength=len(weights)).astype(np.float32)
editable=np.array([.39 < v.co.z < .552 and .10<abs(v.co.x)<.29 for v in mesh.data.vertices])
for iteration in range(8):
    neighbors=np.zeros_like(weights)
    np.add.at(neighbors,left,weights[right]);np.add.at(neighbors,right,weights[left])
    weights[editable]=.60*weights[editable]+.40*(neighbors/np.maximum(degree[:,None],1))[editable]
for index in np.flatnonzero(editable):
    vertex=mesh.data.vertices[int(index)]
    for entry in list(vertex.groups):mesh.vertex_groups[entry.group].remove([int(index)])
    selected=np.argsort(weights[index])[-4:];selected=[i for i in selected if weights[index,i]>.002]
    total=sum(weights[index,i] for i in selected)
    for i in selected:mesh.vertex_groups[names[i]].add([int(index)],float(weights[index,i]/total),'REPLACE')
result = {'sleeve_vertices': len(changed), 'smoothed_vertices': int(editable.sum()), 'face_and_neck': 'original weights', 'eyes': 'original Head weights; no eye deformation'}
