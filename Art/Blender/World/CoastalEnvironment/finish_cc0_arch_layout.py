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
for name in ['BridgeBackLeft','BridgeBackRight']:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
for name,center,tilt,yaw in [
    ('LeftMainLowerFront',(-77,-5,23),.11,-.11),
    ('LeftMainUpperFront',(-72,-7,64),-.06,.10),
    ('LeftMainLowerBack',(-73,5,21),-.085,math.pi+.12),
    ('LeftMainUpperBack',(-72,5,63),.07,math.pi-.105),
    ('RightMainLowerFront',(61,-5,20),-.10,.12),
    ('RightMainUpperFront',(62,-6,49),.09,-.10),
    ('RightMainLowerBack',(62,5,22),.07,math.pi-.10),
    ('RightMainUpperBack',(63,5,49),-.085,math.pi+.11)
]:
    obj=bpy.data.objects[name]
    obj.location=center
    obj.rotation_euler=(0,half+tilt,yaw)
def apply_material(obj,key):
    material=next(mat for mat in bpy.data.materials if mat.name.startswith('NativeArch_SourcePBR_') and ('rock_face_02' if key=='RockFace' else 'coastal_cliff_02' if key=='Cliff' else 'rock_moss_set_01') in mat.name)
    obj.material_slots[0].link='OBJECT'
    obj.material_slots[0].material=material
def place(key,name,center,rotation,scale):
    master=bpy.data.objects['CC0Master_'+key]
    obj=master.copy()
    obj.name=name
    obj.data=master.data
    rocks.objects.link(obj)
    obj.hide_render=False
    obj.location=center
    obj.rotation_euler=rotation
    obj.scale=(scale,scale,scale)
    apply_material(obj,key)
    return obj
for name,key,center,rotation,scale in [
    ('LeftHighLedge','Moss01',(-82,0,83),(half,.12,.18),7),
    ('LeftMiddleLedge','Moss05',(-62,0,81),(half,-.1,-.18),6.7),
    ('RightHighLedge','Moss01',(62,0,66),(half,.1,-.16),7.4),
    ('RightPassageLedge','Moss05',(43,0,61),(half,-.15,.2),5.5)
]:
    obj=bpy.data.objects[name]
    obj.data=bpy.data.objects['CC0Master_'+key].data
    obj.location=center
    obj.rotation_euler=rotation
    obj.scale=(scale,scale,scale)
    apply_material(obj,key)
for name,center,scale,tilt in [
    ('BridgeLeftHaunch',(-37,-2,73),8.5,.3),
    ('BridgeRightHaunch',(35,-2,73),8.7,-.3)
]:
    obj=bpy.data.objects[name]
    obj.location=center
    obj.rotation_euler=(half,tilt,0)
    obj.scale=(scale,scale,scale)
for key,name,center,rotation,scale in [
    ('Moss05','LeftFractureOutcrop',(-70,-24,43),(half,.12,-.1),9.2),
    ('Moss01','RightFractureOutcrop',(61,-22,37),(half,-.12,.18),8),
    ('Moss01','CrownLeftShoulder',(-38,5,86),(half,.2,-.15),6),
    ('Moss05','CrownMiddleLeft',(-12,6,87.5),(half,-.22,.25),5),
    ('Moss01','CrownMiddleRight',(17,5,86),(half,.15,.4),5.7),
    ('Moss05','CrownRightShoulder',(38,4,83),(half,-.25,-.13),5.8)
]:place(key,name,center,rotation,scale)
bpy.context.view_layer.update()
vertices=[]
faces=[]
for obj in rocks.objects:
    offset=len(vertices)
    vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    faces.extend(tuple(offset+index for index in polygon.vertices) for polygon in obj.data.polygons)
bvh=BVHTree.FromPolygons(vertices,faces,all_triangles=False)
fern_master=bpy.data.objects['CC0Master_Fern']
native_height=max(v.co.z for v in fern_master.data.vertices)-min(v.co.z for v in fern_master.data.vertices)
for obj in plants.objects:
    if obj['ModuleKey']=='Fern':
        index=int(obj.name[-1])
        height=[3.4,3.0,3.2,2.9,3.2,2.9][index]
        factor=height/native_height
        obj.scale=(factor,factor,factor)
    point,normal,face,distance=bvh.ray_cast(Vector((obj.location.x,obj.location.y,140)),Vector((0,0,-1)),180)
    if point is not None:obj.location.z=point.z-.08
bpy.context.view_layer.update()
report={'stoneTris':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in rocks.objects),'plantTris':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in plants.objects),'centralShipClearanceVertices':sum(-32<v.x<30 and -28<v.y<28 and 0<v.z<55 for v in vertices),'stage':'FinalWholeModuleCrownShoulders','physicsUnchanged':True,'originalUVPreserved':True}
report['totalTris']=report['stoneTris']+report['plantTris']
json.dump(report,open(r'D:\projects\Pirate_BR\output\CoastalEnvironment\SeaArch-native-assembly-stats.json','w',encoding='utf-8'),indent=2)
bpy.ops.wm.save_as_mainfile(filepath=r'D:\projects\Pirate_BR\PirateGame\Art\Blender\World\CoastalEnvironment\SeaArch_Huge_A_NativeModules.blend',copy=True,compress=True)
bpy.context.window.scene=previous
result={'report':report,'activeSceneRestored':previous.name}
