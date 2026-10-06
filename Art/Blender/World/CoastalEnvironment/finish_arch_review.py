import bpy
import bmesh
import json
import math
import os
from mathutils import Vector, noise
previous_scene=bpy.context.scene
scene=bpy.data.scenes['CoastalArchCReview']
bpy.context.window.scene=scene
body=next(o for o in scene.objects if '_FracturedStone' in o.name)
planes=[(Vector((-1,0,0)),-28),(Vector((1,0,0)),51),(Vector((0,-1,0)),28),(Vector((0,1,0)),6),(Vector((0,0,1)),71)]
def split_polygon(polygon,normal,distance):
    inside=[]
    outside=[]
    for index,current in enumerate(polygon):
        previous=polygon[index-1]
        a=previous.dot(normal)-distance
        b=current.dot(normal)-distance
        if (a<=0)!=(b<=0):
            cut=previous+(current-previous)*(a/(a-b))
            inside.append(cut)
            outside.append(cut)
        (inside if b<=0 else outside).append(current)
    return inside,outside
vertices=[]
faces=[]
lookup={}
def append_polygon(polygon):
    if len(polygon)<3:return
    indices=[]
    for point in polygon:
        key=tuple(round(v,4) for v in point)
        if key not in lookup:
            lookup[key]=len(vertices)
            vertices.append(tuple(point))
        index=lookup[key]
        if not indices or index!=indices[-1]:indices.append(index)
    if len(indices)>2 and indices[0]==indices[-1]:indices.pop()
    if len(set(indices))>2:faces.append(tuple(indices))
removed=0
for polygon in body.data.polygons:
    current=[body.data.vertices[index].co.copy() for index in polygon.vertices]
    kept=[]
    for normal,distance in planes:
        current,outside=split_polygon(current,normal,distance)
        if len(outside)>2:kept.append(outside)
        if len(current)<3:break
    if len(current)>2:removed+=1
    for polygon in kept:append_polygon(polygon)
mesh=bpy.data.meshes.new('SeaArch_Huge_A_C_ReviewApprovedCandidate')
mesh.from_pydata(vertices,[],faces)
mesh.update()
material=body.data.materials[0]
body.data=mesh
bm=bmesh.new()
bm.from_mesh(mesh)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0005)
bmesh.ops.dissolve_degenerate(bm,dist=.0001,edges=list(bm.edges))
for normal,distance in planes:
    boundary=[e for e in bm.edges if e.is_boundary and all(abs(v.co.dot(normal)-distance)<.008 for v in e.verts)]
    if boundary:bmesh.ops.holes_fill(bm,edges=boundary,sides=0)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bmesh.ops.triangulate(bm,faces=list(bm.faces))
bm.to_mesh(mesh)
bm.free()
mesh.materials.append(material)
mesh.update()
changed=0
for vertex in mesh.vertices:
    point=vertex.co.copy()
    normal=vertex.normal.copy()
    if abs(point.x)<48 or point.z<12 or point.z>80 or abs(normal.z)>.56:
        continue
    column=math.floor(point.x/9.7+.18*math.sin(point.y*.17))
    phase=math.sin(column*1.724+point.y*.061)
    second=math.sin(column*2.13+point.y*.074)
    breaks=(21+phase*5.3,42+second*6.2,64-phase*6.4)
    distance=min(abs(point.z-z-point.x*.029*math.sin(column*2)) for z in breaks)
    width=1.45+.40*math.sin(column*1.1)
    crease=max(0,1-distance/width)
    interrupted=max(.18,.52+.48*math.sin(point.y*.25+column*.83))
    depth=2.65*crease*interrupted
    if depth>.05:
        vertex.co-=normal*depth
        changed+=1
mesh.update()
uv=mesh.uv_layers.new(name='UVMap')
for polygon in mesh.polygons:
    normal=polygon.normal
    axis=max(range(3),key=lambda a:abs(normal[a]))
    for index in polygon.loop_indices:
        point=mesh.vertices[mesh.loops[index].vertex_index].co
        coordinate=(point.y,point.z) if axis==0 else (point.x,point.z) if axis==1 else (point.x,point.y)
        uv.data[index].uv=(coordinate[0]*.075,coordinate[1]*.075)
    polygon.use_smooth=True
nodes=material.node_tree.nodes
links=material.node_tree.links
diffuse=next(n for n in nodes if n.type=='TEX_IMAGE' and 'diff_' in n.image.name)
tint=next(n for n in nodes if n.type=='MIX_RGB')
tint.inputs[0].default_value=.23
tint.inputs[2].default_value=(.79,.72,.59,1)
gain=nodes.new('ShaderNodeMixRGB')
gain.name='DryLimestoneBitmapGain'
gain.blend_type='MULTIPLY'
gain.inputs[0].default_value=1
gain.inputs[2].default_value=(1.56,1.51,1.42,1)
links.new(diffuse.outputs['Color'],gain.inputs[1])
links.new(gain.outputs[0],tint.inputs[1])
material['DryAlbedoGain']=[1.56,1.51,1.42]
material['BaseTintMix']=.23
material['BaseTint']=[.79,.72,.59]
material['TransverseFractureDepthMetres']=2.65
parameter_path=r'D:\projects\Pirate_BR\output\CoastalEnvironment\SeaArch_Huge_A-C-material-parameters.json'
parameters=json.load(open(parameter_path,encoding='utf-8'))
parameters['dryAlbedoGain']=[1.56,1.51,1.42]
parameters['baseTintMix']=.23
parameters['baseTint']=[.79,.72,.59]
parameters['transverseFractureDepthMetres']=2.65
json.dump(parameters,open(parameter_path,'w',encoding='utf-8'),indent=2)
save_path=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\SeaArch_Huge_A_CReview.blend'
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=save_path,copy=True,compress=True)
bpy.context.window.scene=previous_scene
result={'saved':save_path,'removedThinFragments':removed,'transverseReliefVertices':changed,'stoneTris':len(mesh.polygons),'otherModelsChanged':False}
