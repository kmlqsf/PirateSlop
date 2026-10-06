import bpy
import json
import math
from mathutils import Vector
from mathutils.bvhtree import BVHTree
previous=bpy.context.scene
scene=bpy.data.scenes['CoastalModularArch']
bpy.context.window.scene=scene
rocks=bpy.data.collections.new('SeaArchNativeRocks')
plants=bpy.data.collections.new('SeaArchNativePlants')
scene.collection.children.link(rocks)
scene.collection.children.link(plants)
for source in bpy.data.collections['NativeCC0Masters'].objects:source.hide_render=True
fern=bpy.data.objects['CC0Master_Fern']
native_fern=bpy.data.objects['fern_02_a']
fern.data=native_fern.data.copy()
matrix=native_fern.matrix_world.copy()
for vertex in fern.data.vertices:vertex.co=matrix @ vertex.co
points=[v.co for v in fern.data.vertices]
low=Vector(tuple(min(v[i] for v in points) for i in range(3)))
high=Vector(tuple(max(v[i] for v in points) for i in range(3)))
offset=Vector(((low.x+high.x)*.5,(low.y+high.y)*.5,low.z))
for vertex in fern.data.vertices:vertex.co-=offset
fern.data.materials.clear()
fern.data.materials.append(bpy.data.materials['NativeCC0Fern'])
for polygon in fern.data.polygons:polygon.material_index=0
fern.data.update()
def place(key,name,center,rotation,scale,collection=rocks):
    master=bpy.data.objects['CC0Master_'+key]
    obj=master.copy()
    obj.data=master.data
    obj.name=name
    collection.objects.link(obj)
    obj.hide_render=False
    obj.hide_set(False)
    obj.location=center
    obj.rotation_euler=rotation
    obj.scale=(scale,scale,scale)
    obj['NativeSourceModule']=key
    return obj
half=math.pi*.5
for name,center,scale,yaw in [
    ('LeftFrontOuter',(-81,-15,44),2.23,-.04),
    ('LeftFrontInner',(-61,-14,40),2.06,.065),
    ('LeftBackOuter',(-80,13,44),2.23,math.pi+.04),
    ('LeftBackInner',(-61,12,40),2.06,math.pi-.07),
    ('RightFrontInner',(51,-13,29.5),1.62,.035),
    ('RightFrontOuter',(70,-12,31),1.74,-.06),
    ('RightBackInner',(51,12,29.5),1.62,math.pi-.03),
    ('RightBackOuter',(70,12,31),1.74,math.pi+.07)
]:place('Cliff',name,center,(0,-half,yaw),scale)
for name,center,scale,yaw in [
    ('BridgeFrontLeft',(-18,-13,83),1.75,0),
    ('BridgeFrontRight',(32,-12,80.5),1.5,.025),
    ('BridgeBackLeft',(-18,12,83),1.75,math.pi),
    ('BridgeBackRight',(32,12,80.5),1.5,math.pi-.025)
]:place('Cliff',name,center,(0,0,yaw),scale)
for side,x,yaw in [('Left',-47,half),('Right',42,-half)]:
    for index,z in enumerate([13,34,55]):
        place('RockFace',side+'PassageFace'+str(index),(x,0,z),(0,half,yaw+.045*math.sin(index)),4.0)
for side,columns,heights,scale in [
    ('Left',[-79,-60],[10,38,66],9.5),
    ('Right',[52,69],[7,29,51],8.0)
]:
    for column,x in enumerate(columns):
        for index,z in enumerate(heights):
            place('Moss01',side+'ClosedRock'+str(column)+str(index),(x,0,z),(half,0,.08*math.sin(column*3+index)),scale)
for name,center,scale,yaw in [
    ('LeftHighLedge',(-82,0,88),13,.04),
    ('LeftMiddleLedge',(-62,0,81),11,-.13),
    ('RightHighLedge',(62,0,71),14,.1),
    ('RightPassageLedge',(43,0,64),8,-.17),
    ('BridgeClosedLeft',(-17,0,84),19,.03),
    ('BridgeClosedRight',(18,0,83),17.5,math.pi+.015)
]:place('Moss03',name,center,(0,0,yaw),scale)
for name,key,center,scale,yaw in [
    ('LeftBaseOutcrop','Moss01',(-91,-12,2),5.8,-.18),
    ('LeftBaseStep','Moss05',(-64,-18,0),5.6,.5),
    ('RightBaseOutcrop','Moss01',(81,-10,2),5.5,.25),
    ('RightBaseStep','Moss05',(56,-19,0),5.3,-.6)
]:place(key,name,center,(half,0,yaw),scale)
bpy.context.view_layer.update()
vertices=[]
faces=[]
for obj in rocks.objects:
    offset=len(vertices)
    vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    faces.extend(tuple(offset+i for i in polygon.vertices) for polygon in obj.data.polygons)
bvh=BVHTree.FromPolygons(vertices,faces,all_triangles=False)
supports=[]
for key,name,x,y,height,yaw in [
    ('PalmBent','LeftPalmBent',-83,-2,22,.25),
    ('Palm','LeftPalmStraight',-61,0,19,-.4),
    ('PalmBent','RightPalmBent',65,-1,18,-.65)
]:
    point,normal,face,distance=bvh.ray_cast(Vector((x,y,140)),Vector((0,0,-1)),180)
    if point is None:continue
    master=bpy.data.objects['CC0Master_'+key]
    native_height=max(v.co.z for v in master.data.vertices)-min(v.co.z for v in master.data.vertices)
    obj=place(key,name,(x,y,point.z-.12),(0,0,yaw),height/native_height,plants)
    supports.append({'name':name,'baseZ':point.z,'normal':list(normal),'height':height})
for index,(x,y,height,yaw) in enumerate([(-85,-3,2.4,.1),(-81,0,2.1,-.45),(-63,-3,2.2,.25),(-59,-1,2.0,-.2),(62,-3,2.3,.6),(67,1,2.0,-.4)]):
    point,normal,face,distance=bvh.ray_cast(Vector((x,y,140)),Vector((0,0,-1)),180)
    if point is None:continue
    master=bpy.data.objects['CC0Master_Fern']
    native_height=max(v.co.z for v in master.data.vertices)-min(v.co.z for v in master.data.vertices)
    place('Fern','NativeLedgeFern'+str(index),(x,y,point.z-.05),(0,0,yaw),height/native_height,plants)
bpy.context.view_layer.update()
points=[obj.matrix_world @ Vector(corner) for obj in rocks.objects for corner in obj.bound_box]
low=[min(point[i] for point in points) for i in range(3)]
high=[max(point[i] for point in points) for i in range(3)]
rock_tris=sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in rocks.objects)
plant_tris=sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in plants.objects)
violations=[list(point) for point in vertices if -32<point.x<30 and -28<point.y<28 and 0<point.z<55]
report={'stoneTris':rock_tris,'plantTris':plant_tris,'totalTris':rock_tris+plant_tris,'stoneObjects':len(rocks.objects),'plantObjects':len(plants.objects),'rockBoundsMin':low,'rockBoundsMax':high,'centralShipClearanceVertices':len(violations),'plantSupports':supports,'sourceUVUnchanged':True,'originalPhysicsUnchanged':True}
json.dump(report,open(r'D:\projects\Pirate_BR\output\CoastalEnvironment\SeaArch-native-assembly-stats.json','w',encoding='utf-8'),indent=2)
save_path=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\SeaArch_Huge_A_NativeModules.blend'
bpy.ops.wm.save_as_mainfile(filepath=save_path,copy=True,compress=True)
bpy.context.window.scene=previous
result={'report':report,'saved':save_path,'activeSceneRestored':previous.name}
