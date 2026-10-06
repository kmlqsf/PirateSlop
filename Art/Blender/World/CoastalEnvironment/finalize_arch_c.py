import bpy
import bmesh
import json
import math
import os
from mathutils import Vector
from mathutils.bvhtree import BVHTree
original_scene=bpy.context.scene
COASTAL_KEEP_SCENE=True
COASTAL_PROTOTYPE=True
COASTAL_SELECTION=['SeaArch_Huge_A']
path=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\build_coastal_c.py'
exec(compile(open(path,encoding='utf-8').read().split('reports = []')[0],path,'exec'))
review=bpy.data.scenes['CoastalArchCReview']
bpy.context.window.scene=review
scene=review
body=next(o for o in review.objects if '_FracturedStone' in o.name)
planes=[(Vector((-1,0,0)),-35.8),(Vector((1,0,0)),44.3),(Vector((0,-1,0)),22),(Vector((0,1,0)),-10),(Vector((0,0,1)),72)]
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
mesh=bpy.data.meshes.new('SeaArch_Huge_A_FinalTexturedStone')
mesh.from_pydata(vertices,[],faces)
mesh.update()
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
mesh.materials.append(stone)
uv=mesh.uv_layers.new(name='UVMap')
for polygon in mesh.polygons:
    normal=polygon.normal
    axis=max(range(3),key=lambda a:abs(normal[a]))
    for index in polygon.loop_indices:
        point=mesh.vertices[mesh.loops[index].vertex_index].co
        coordinate=(point.y,point.z) if axis==0 else (point.x,point.z) if axis==1 else (point.x,point.y)
        uv.data[index].uv=(coordinate[0]*.075,coordinate[1]*.075)
    polygon.use_smooth=True
activate([body])
mesh.calc_loop_triangles()
if len(mesh.loop_triangles)>24000:
    reduction=body.modifiers.new('ArchReviewGeometryBudget','DECIMATE')
    reduction.ratio=24000/len(mesh.loop_triangles)
    bpy.ops.object.modifier_apply(modifier=reduction.name)
mesh=body.data
palms=[o for o in review.objects if o.get('FoliageClass')=='Palm']
for original in palms:
    matrix=original.matrix_world.copy()
    name=original.name.split('_Palm')[0]
    height=original['PalmHeight']
    bend=original['PalmBend']
    bpy.data.objects.remove(original,do_unlink=True)
    palm=feather_palm(name,height,bend)
    palm.matrix_world=matrix
material=bpy.data.materials.get('CoastalPalm')
nodes=material.node_tree.nodes
links=material.node_tree.links
shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
for link in list(shader.inputs['Alpha'].links):links.remove(link)
shader.inputs['Alpha'].default_value=1
color_input=shader.inputs['Base Color']
incoming=color_input.links[0].from_socket
bright=nodes.get('PalmLeafExposure') or nodes.new('ShaderNodeMixRGB')
bright.name='PalmLeafExposure'
bright.blend_type='MULTIPLY'
bright.inputs[0].default_value=1
links.new(incoming,bright.inputs[1])
bright.inputs[2].default_value=(3.2,3.7,2.4,1)
links.new(bright.outputs[0],color_input)
material['GeometricFrondsOpaque']=True
material['LeafColourGain']=[3.2,3.7,2.4]
template=bpy.data.objects.get('Template_Fern')
tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[tuple(p.vertices) for p in mesh.polygons])
ferns=[o for o in review.objects if 'Fern' in o.name]
for plant in ferns:plant.scale*=1.65
positions=[(-71,-23),(-42,-22),(-21,-15),(45,-22),(70,-24)]
for index,(x,y) in enumerate(positions):
    hit,normal,face,distance=tree.ray_cast(Vector((x,y,130)),Vector((0,0,-1)),150)
    if hit is None or normal.z<.5:continue
    for j,offset in enumerate((Vector((-2,0,0)),Vector((2,1,0)))):
        plant=template.copy()
        plant.data=template.data.copy()
        plant.name='SeaArch_Huge_A_FinalLedgeFern'+str(index)+'_'+str(j)
        plant.location=hit+offset-Vector((0,0,.12))
        plant.scale=(6.5,6.5,6.5)
        plant.rotation_euler.z=index*.91+j*1.6
        plant.hide_render=False
        review.collection.objects.link(plant)
        plant.hide_set(False)
        activate([plant])
        reduction=plant.modifiers.new('FernReviewBudget','DECIMATE')
        reduction.ratio=.05
        bpy.ops.object.modifier_apply(modifier=reduction.name)
for obj in review.objects:
    if obj.type=='MESH':obj.hide_render=False
review['FinalReview']='Bitmap rock wall texture and tangent normal, geometric opaque fronds, local ledge ferns, no hanging union spike.'
bpy.context.view_layer.update()
for obj in review.objects:
    for material in obj.data.materials:
        for node in material.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image and node.image.packed_file is None:node.image.pack()
save_path=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\SeaArch_Huge_A_CReview.blend'
bpy.ops.wm.save_as_mainfile(filepath=save_path,copy=True,compress=True)
bpy.context.window.scene=original_scene
result={'saved':save_path,'removedSpikeFaces':removed,'stoneTris':sum(len(p.vertices)-2 for p in body.data.polygons),'plants':len([o for o in review.objects if o!=body]),'otherModelsChanged':False}
