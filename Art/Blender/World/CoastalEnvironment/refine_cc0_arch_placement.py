import bpy
import math
import json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
previous=bpy.context.scene
scene=bpy.data.scenes['CoastalModularArch']
bpy.context.window.scene=scene
rocks=bpy.data.collections['SeaArchNativeRocks']
plants=bpy.data.collections['SeaArchNativePlants']
half=math.pi*.5
for name in ['LeftPassageFace0','LeftPassageFace1','LeftPassageFace2','RightPassageFace0','RightPassageFace1','RightPassageFace2']:
    bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
for name,center,scale in [
    ('LeftFrontOuter',(-94,-13,44),1.95),
    ('LeftFrontInner',(-53,-13,40),1.8),
    ('LeftBackOuter',(-94,13,44),1.95),
    ('LeftBackInner',(-53,12,40),1.8),
    ('RightFrontInner',(44,-10,30),1.5),
    ('RightFrontOuter',(79,-10,30),1.47),
    ('RightBackInner',(44,12,30),1.5),
    ('RightBackOuter',(79,12,30),1.47)
]:
    obj=bpy.data.objects[name]
    obj.location=center
    obj.scale=(scale,scale,scale)
def module(key,name,center,rotation,scale):
    source=bpy.data.objects['CC0Master_'+key]
    obj=source.copy()
    obj.data=source.data
    obj.name=name
    rocks.objects.link(obj)
    obj.hide_render=False
    obj.location=center
    obj.rotation_euler=rotation
    obj.scale=(scale,scale,scale)
    obj['NativeSourceModule']=key
    return obj
for name,center,scale,yaw in [
    ('LeftMainLowerFront',(-75,-6,20),10.9,-.06),
    ('LeftMainUpperFront',(-73,-6,62),12.9,.045),
    ('LeftMainLowerBack',(-74,6,20),10.9,math.pi+.055),
    ('LeftMainUpperBack',(-72,6,62),12.9,math.pi-.045),
    ('RightMainLowerFront',(61,-5,21),10.6,.04),
    ('RightMainUpperFront',(61,-5,50),9.9,-.055),
    ('RightMainLowerBack',(62,5,21),10.6,math.pi-.04),
    ('RightMainUpperBack',(62,5,50),9.9,math.pi+.055)
]:module('RockFace',name,center,(0,half,yaw),scale)
for name,z in [('LeftHighLedge',81),('LeftMiddleLedge',75),('RightHighLedge',65),('RightPassageLedge',60)]:
    bpy.data.objects[name].location.z=z
module('Moss01','BridgeLeftHaunch',(-39,0,80),(half,0,-.1),8)
module('Moss01','BridgeRightHaunch',(40,0,78),(half,0,.12),8)
materials={}
for obj in rocks.objects:
    source=obj.data.materials[0]
    if source.name not in materials:
        material=source.copy()
        material.name='NativeArch_'+source.name
        nodes=material.node_tree.nodes
        links=material.node_tree.links
        image=next(node for node in nodes if node.type=='TEX_IMAGE' and '_diff_2k' in node.image.name)
        destinations=[link.to_socket for link in list(links) if link.from_socket==image.outputs['Color']]
        correction=nodes.new('ShaderNodeHueSaturation')
        correction.name='NativeBitmapPaletteCorrection'
        correction.inputs['Saturation'].default_value=.55 if 'moss' in source.name else .82 if 'face' in source.name else .88
        correction.inputs['Value'].default_value=1.14 if 'moss' in source.name else 1.06
        links.new(image.outputs['Color'],correction.inputs['Color'])
        for destination in destinations:links.new(correction.outputs['Color'],destination)
        material['PreserveNativeBitmapDetail']=True
        materials[source.name]=material
    obj.material_slots[0].link='OBJECT'
    obj.material_slots[0].material=materials[source.name]
palm_material=bpy.data.materials['NativeCC0Palm']
nodes=palm_material.node_tree.nodes
links=palm_material.node_tree.links
diffuse=next(node for node in nodes if node.type=='TEX_IMAGE' and 'diffuse' in node.image.name)
shader=next(node for node in nodes if node.type=='BSDF_PRINCIPLED')
separate=nodes.new('ShaderNodeSeparateColor')
links.new(diffuse.outputs['Color'],separate.inputs['Color'])
red=nodes.new('ShaderNodeMath')
red.operation='MULTIPLY'
red.inputs[1].default_value=.95
links.new(separate.outputs['Red'],red.inputs[0])
green=nodes.new('ShaderNodeMath')
green.operation='GREATER_THAN'
links.new(separate.outputs['Green'],green.inputs[0])
links.new(red.outputs[0],green.inputs[1])
gain=nodes.new('ShaderNodeMixRGB')
gain.blend_type='MULTIPLY'
gain.inputs[0].default_value=1
gain.inputs[2].default_value=(1.6,2.4,1.35,1)
links.new(diffuse.outputs['Color'],gain.inputs[1])
mix=nodes.new('ShaderNodeMixRGB')
links.new(green.outputs[0],mix.inputs[0])
links.new(diffuse.outputs['Color'],mix.inputs[1])
links.new(gain.outputs[0],mix.inputs[2])
links.new(mix.outputs[0],shader.inputs['Base Color'])
palm_material['GreenLeafGain']=[1.6,2.4,1.35]
bpy.context.view_layer.update()
vertices=[]
faces=[]
for obj in rocks.objects:
    offset=len(vertices)
    vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    faces.extend(tuple(offset+index for index in polygon.vertices) for polygon in obj.data.polygons)
bvh=BVHTree.FromPolygons(vertices,faces,all_triangles=False)
for obj in plants.objects:
    point,normal,face,distance=bvh.ray_cast(Vector((obj.location.x,obj.location.y,140)),Vector((0,0,-1)),180)
    if point is not None:obj.location.z=point.z-.08
bpy.context.view_layer.update()
report={'stoneTris':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in rocks.objects),'plantTris':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in plants.objects),'centralShipClearanceVertices':sum(-32<v.x<30 and -28<v.y<28 and 0<v.z<55 for v in vertices),'stage':'WholeNativeModuleOverlapRefinement','originalPhysicsUnchanged':True}
json.dump(report,open(r'D:\projects\Pirate_BR\output\CoastalEnvironment\SeaArch-native-assembly-stats.json','w',encoding='utf-8'),indent=2)
bpy.ops.wm.save_as_mainfile(filepath=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\SeaArch_Huge_A_NativeModules.blend',copy=True,compress=True)
bpy.context.window.scene=previous
result={'report':report,'activeSceneRestored':previous.name}
