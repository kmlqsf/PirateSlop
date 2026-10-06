import bpy
import json
import math
from datetime import datetime
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(r'D:\projects\Pirate_BR')
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
OUTPUT=ROOT/'output'/'CoastalEnvironment'
profiles=json.loads((OUTPUT/'physical-shape-profiles.json').read_text(encoding='utf-8-sig'))
components={record['name']:record['components'] for record in json.loads((OUTPUT/'physical-component-bounds.json').read_text(encoding='utf-8-sig'))}
previous=bpy.context.window.scene
checkpoint=None
if bpy.data.is_dirty:
    checkpoint=SOURCE/('SeaArch_UserCheckpoint_'+datetime.now().strftime('%Y%m%d_%H%M%S')+'.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(checkpoint),copy=True,compress=True)
scene=bpy.data.scenes.new('TripoCoastalCollection')
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
groups={}
manifest=[]
PALM_MAT=bpy.data.materials['TripoArch_NativePalm']
FERN_MAT=bpy.data.materials['TripoArch_NativeFern']
FERN_MESH=bpy.data.meshes['TripoArch_PreparedFernMesh']

def activate(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]

def collection(name):
    coll=bpy.data.collections.new('TripoSet_'+name)
    scene.collection.children.link(coll)
    groups[name]=coll
    return coll

def fit(coll,name,source_id,center,size,angles=(0,0,0)):
    source=bpy.data.objects['TripoMaster_'+source_id]
    obj=source.copy()
    obj.name=coll.name+'_'+name
    obj.parent=None
    obj.matrix_world=Matrix.Identity(4)
    obj.rotation_mode='XYZ'
    coll.objects.link(obj)
    obj.hide_render=False
    obj.hide_set(False)
    obj.rotation_euler=tuple(math.radians(a) for a in angles)
    rotation=obj.rotation_euler.to_matrix()
    native=Vector(tuple(max(v.co[i] for v in obj.data.vertices)-min(v.co[i] for v in obj.data.vertices) for i in range(3)))
    obj.scale=tuple(size[i]/max(.001,native[i]) for i in range(3))
    bpy.context.view_layer.update()
    points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
    actual=Vector(tuple(max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)))
    factor=min(size[i]/max(.001,actual[i]) for i in range(3))
    obj.scale*=factor
    bpy.context.view_layer.update()
    points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
    origin=(Vector(tuple(min(p[i] for p in points) for i in range(3)))+Vector(tuple(max(p[i] for p in points) for i in range(3))))*.5
    obj.location=Vector(center)-origin
    obj['source_glb']=source_id
    obj['model_name']=coll.name.removeprefix('TripoSet_')
    obj['role']='Rock'
    return obj

def body(coll,label,source_id,center,size,yaw=0):
    return fit(coll,label,source_id,center,size,(0,0,yaw))

def bvh_for(coll):
    vertices=[]
    faces=[]
    bpy.context.view_layer.update()
    for obj in coll.objects:
        if obj.type=='MESH' and obj.get('role')=='Rock':
            offset=len(vertices)
            vertices.extend(obj.matrix_world@v.co for v in obj.data.vertices)
            faces.extend(tuple(offset+i for i in polygon.vertices) for polygon in obj.data.polygons)
    return BVHTree.FromPolygons(vertices,faces,all_triangles=False)

def plant(coll,label,key,point,height,yaw=0):
    source=bpy.data.objects['CC0Master_'+key]
    obj=source.copy()
    obj.data=FERN_MESH if key=='Fern' else source.data
    obj.name=coll.name+'_'+label
    obj.parent=None
    obj.matrix_world=Matrix.Identity(4)
    obj.rotation_mode='XYZ'
    obj.rotation_euler=(0,0,math.radians(yaw))
    coll.objects.link(obj)
    obj.hide_render=False
    obj.hide_set(False)
    low=min(v.co.z for v in obj.data.vertices)
    high=max(v.co.z for v in obj.data.vertices)
    scale=height/max(.001,high-low)
    obj.scale=(scale,scale,scale)
    feet=[v.co for v in obj.data.vertices if v.co.z<=low+(high-low)*.012]
    anchor=Vector((sum(v.x for v in feet)/len(feet),sum(v.y for v in feet)/len(feet),low))
    obj.location=Vector(point)-(obj.rotation_euler.to_matrix()@anchor)*scale
    for slot in obj.material_slots:
        slot.link='OBJECT'
        slot.material=FERN_MAT if key=='Fern' else PALM_MAT
    obj['model_name']=coll.name.removeprefix('TripoSet_')
    obj['role']='Fern' if key=='Fern' else 'Palm'
    obj['native_plant_source']=key
    return obj

def supported_plants(coll,settings):
    bvh=bvh_for(coll)
    for label,key,x,y,height,yaw in settings:
        point=None
        for dx,dy in [(0,0),(2,0),(-2,0),(0,2),(0,-2)]:
            hit,normal,face,distance=bvh.ray_cast(Vector((x+dx,y+dy,200)),Vector((0,0,-1)),300)
            if hit is not None and normal.z>.3:
                point=hit
                break
        if point is not None:
            point.z-=.05
            plant(coll,label,key,point,height,yaw)

def foot(coll,label,center,size,index):
    body(coll,label,'14_RockFragment_A' if index%2==0 else '15_RockFragment_B',center,size,(-21,16,38,-12)[index%4])

try:
    bpy.context.window.scene=scene
    arch=collection('SeaArch_Huge_A')
    for source in bpy.data.scenes['TripoArchScene'].objects:
        if source.type=='MESH' and ('source_glb' in source or 'native_plant_source' in source):
            obj=source.copy()
            obj.name=arch.name+'_'+source.name
            arch.objects.link(obj)
            obj.hide_render=False
            obj.hide_set(False)
            obj['model_name']='SeaArch_Huge_A'
            obj['role']='Rock' if 'source_glb' in source else 'Fern' if source['native_plant_source']=='Fern' else 'Palm'
    lagoon=collection('Sea_Lagoon_Cave')
    for label,source_id,center,size,yaw in [
        ('NorthWestMass','01_RockMass_A',(-65,65,32),(58,53,100),-4),
        ('NorthEastMass','02_RockMass_B',(67,65,28),(58,50,88),5),
        ('SouthWestMass','03_RockMass_C',(-66,-63,29),(58,58,99),3),
        ('SouthEastMass','01_RockMass_A',(63,-64,29),(50,51,97),-3),
        ('NorthWestRib','04_RockMonolith_A',(-63,50,44),(29,24,79),-7),
        ('NorthEastRib','05_RockMonolith_B',(62,55,34),(31,26,82),6),
        ('SouthWestRib','06_RockMonolith_C',(-63,-53,37),(29,26,90),-5),
        ('SouthEastRib','04_RockMonolith_A',(67,-70,39),(30,28,80),7),
        ('NorthWestFoot','09_RockWedge_A',(-85,70,3),(35,37,22),8),
        ('NorthEastFoot','10_RockWedge_B',(85,70,3),(40,37,20),-11),
        ('SouthWestFoot','13_RockBoulder_A',(-85,-77,3),(35,38,21),16),
        ('SouthEastFoot','08_RockTerrace_B',(83,-80,3),(44,35,18),-7),
        ('NorthBridgeLeft','11_RockElongated_A',(-26,84,52),(50,36,38),-4),
        ('NorthBridgeMiddle','02_RockMass_B',(13,92,56),(52,32,43),5),
        ('NorthBridgeRight','08_RockTerrace_B',(48,81,61),(50,36,37),-6),
        ('SouthBridgeLeft','12_RockElongated_B',(-28,-78,61),(55,38,32),5),
        ('SouthBridgeMiddle','03_RockMass_C',(12,-92,58),(60,35,36),-4),
        ('SouthBridgeRight','07_RockTerrace_A',(48,-79,62),(45,35,32),8),
        ('WestBridgeNorth','01_RockMass_A',(-89,29,56),(50,52,37),-3),
        ('WestBridgeSouth','09_RockWedge_A',(-97,-18,54),(49,53,40),7),
        ('EastBridgeSouth','02_RockMass_B',(85,-9,57),(50,53,39),-5),
        ('EastBridgeNorth','10_RockWedge_B',(87,32,58),(47,51,40),6)
    ]:
        body(lagoon,label,source_id,center,size,yaw)
    for index,(x,y) in enumerate([(-102,70),(102,76),(-100,-84),(104,-88),(-84,42),(82,-46)]):
        foot(lagoon,'Toe'+str(index),(x,y,1),(8+index%3,9,6),index)
    supported_plants(lagoon,[('PalmNW','PalmBent',-62,66,18,15),('PalmSW','Palm',-66,-63,20,-22),('PalmNE','PalmBent',67,64,17,-36),('FernNW','Fern',-68,62,3,18),('FernNW2','Fern',-64,65,2.6,-15),('FernSW','Fern',-71,-61,3.1,48),('FernNE','Fern',62,64,3,31),('FernSE','Fern',66,-63,2.8,-27)])
    moai=collection('Reef_Moai_A')
    body(moai,'MainMass','02_RockMass_B',(1,-2,-.5),(38,42,34),-8)
    body(moai,'FrontShoulder','09_RockWedge_A',(-9,-16,-1),(25,22,24),11)
    body(moai,'OuterFoot','13_RockBoulder_A',(14,10,-4),(25,26,21),-17)
    foot(moai,'OuterChip',(-20,4,-2),(9,10,8),0)
    for name,count,footcount in [('Reef_Spires_A',5,3),('Reef_Spires_B',8,5),('Reef_Spires_C',10,6)]:
        coll=collection(name)
        candidates=[c for c in components[name] if c['high'][2]>5 and min(c['high'][i]-c['low'][i] for i in (0,1))>2]
        candidates.sort(key=lambda c:c['high'][2],reverse=True)
        chosen=[]
        for comp in candidates:
            center=[(comp['low'][i]+comp['high'][i])*.5 for i in range(3)]
            extent=[comp['high'][i]-comp['low'][i] for i in range(3)]
            if any(math.hypot(center[0]-p[0],center[1]-p[1])<max(2.4,min(extent[:2])*.38) for p in chosen):
                continue
            chosen.append(center)
            low=list(comp['low'])
            high=list(comp['high'])
            low[2]=max(-4,low[2])
            extent=[high[i]-low[i] for i in range(3)]
            center=[(high[i]+low[i])*.5 for i in range(3)]
            index=len(chosen)-1
            source_id=['04_RockMonolith_A','05_RockMonolith_B','06_RockMonolith_C'][index%3]
            if extent[2]>28 and extent[2]/min(extent[:2])>3:
                height=extent[2]*.64
                body(coll,'RibLower'+str(index),source_id,(center[0],center[1],low[2]+height*.5),(extent[0],extent[1],height),(-3,4,7)[index%3])
                body(coll,'RibUpper'+str(index),['05_RockMonolith_B','06_RockMonolith_C','04_RockMonolith_A'][index%3],(center[0],center[1],high[2]-height*.5),(extent[0]*.95,extent[1]*.95,height),(5,-4,3)[index%3])
            else:
                body(coll,'Rib'+str(index),source_id,center,extent,(-4,6,-8)[index%3])
            if len(chosen)>=count:
                break
        shallow=[c for c in components[name] if c['high'][2]<3 and c['high'][2]>-2 and c['low'][2]<0]
        shallow.sort(key=lambda c:(c['high'][0]-c['low'][0])*(c['high'][1]-c['low'][1]),reverse=True)
        for index,comp in enumerate(shallow[:footcount]):
            low=comp['low'];high=comp['high']
            body(coll,'SubmergedFoot'+str(index),['07_RockTerrace_A','08_RockTerrace_B','13_RockBoulder_A'][index%3],[(low[i]+high[i])*.5 for i in range(3)],[high[i]-low[i] for i in range(3)],index*13-9)
        if chosen:
            x,y,z=chosen[0]
            supported_plants(coll,[('LedgeFern','Fern',x,y,1.1 if name=='Reef_Spires_A' else 1.6,27)])
    shallow=collection('Reef_ShallowField_A')
    selected=sorted(components['Reef_ShallowField_A'],key=lambda c:c['high'][2],reverse=True)[:13]
    for index,comp in enumerate(selected):
        low=comp['low'];high=comp['high']
        body(shallow,'SubmergedRock'+str(index),['07_RockTerrace_A','13_RockBoulder_A','14_RockFragment_A','15_RockFragment_B'][index%4],[(low[i]+high[i])*.5 for i in range(3)],[high[i]-low[i] for i in range(3)],index*17-10)
    large=collection('RockLarge')
    body(large,'MainTerrace','07_RockTerrace_A',(-.04,.04,1.50),(7.47,5.27,3.42),-7)
    body(large,'InsetFragment','15_RockFragment_B',(-1.7,-1.2,.6),(1.7,1.5,1.3),19)
    medium=collection('RockMedium')
    body(medium,'MainRock','12_RockElongated_B',(-.12,-.01,.74),(4.30,3.31,1.88),11)
    for name,variant in [('CliffWallA',0),('CliffWallB',1),('CliffWallC',2)]:
        coll=collection(name)
        if variant<2:
            body(coll,'MainCliff',['01_RockMass_A','02_RockMass_B'][variant],(-.12,0,5.0),(12.2,6.75,10.4),(-5,8)[variant])
            body(coll,'BaseWedge',['09_RockWedge_A','10_RockWedge_B'][variant],(-3.5,-1.1,1.0),(5,4.5,2.4),12)
        else:
            body(coll,'LeftRib','05_RockMonolith_B',(-2.7,.15,4.8),(6.8,6.5,10.4),-6)
            body(coll,'RightRib','06_RockMonolith_C',(2.8,.1,4.0),(6.5,6.3,8.8),7)
            body(coll,'LocalTerrace','08_RockTerrace_B',(-.5,-1.0,3.3),(5.7,4.7,2.8),-9)
        supported_plants(coll,[('LedgeFern','Fern',-1.7,.3,.65,27)])
    for name,key,height,yaw in [('Palm','Palm',7.379,0),('PalmBent','PalmBent',6.346,180),('PalmYoung','Palm',4.796,-22)]:
        coll=collection(name)
        plant(coll,'NativePalm',key,(0,0,0),height,yaw)
    bush=collection('Bush')
    for index,(point,height,yaw) in enumerate([((-.33,0,0),1.10,-24),((.31,.12,0),1.33,46),((0,-.26,0),1.16,117)]):
        plant(bush,'NativeCluster'+str(index),'Fern',point,height,yaw)
    fern=collection('Fern')
    plant(fern,'NativeRosette','Fern',(0,0,0),.76,14)
    protected={'SeaArch_Huge_A':[[[-32,-28,0],[30,28,55]]],'Sea_Lagoon_Cave':[[[-20,-25,0],[20,25,95]],[[-10,-140,0],[10,140,27]],[[-145,-15,0],[145,15,30]]]}
    bpy.context.view_layer.update()
    for name,coll in groups.items():
        records=[]
        for obj in coll.objects:
            points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
            low=[min(p[i] for p in points) for i in range(3)]
            high=[max(p[i] for p in points) for i in range(3)]
            if obj['role']=='Rock':
                for a,b in protected.get(name,[]):
                    if all(high[i]>a[i] and low[i]<b[i] for i in range(3)):
                        raise RuntimeError(name+' protected passage overlaps '+obj.name)
            records.append({'name':obj.name,'role':obj['role'],'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'bounds':[low,high],'source':obj.get('source_glb',obj.get('native_plant_source'))})
        manifest.append({'name':name,'collection':coll.name,'triangles':sum(record['triangles'] for record in records),'objects':len(records),'bounds':[[min(record['bounds'][0][i] for record in records) for i in range(3)],[max(record['bounds'][1][i] for record in records) for i in range(3)]],'protectedPassages':protected.get(name,[]),'protectedPassagesPassed':True,'objectsDetail':records})
        coll.hide_render=name!='SeaArch_Huge_A'
        coll.hide_viewport=name!='SeaArch_Huge_A'
    source_scene=bpy.data.scenes['TripoArchScene']
    scene.world=source_scene.world.copy()
    for source in source_scene.objects:
        if source.type in {'LIGHT','CAMERA'}:
            obj=source.copy()
            obj.data=source.data.copy()
            obj.name='TripoCollection_'+source.name
            scene.collection.objects.link(obj)
            if source==source_scene.camera:
                scene.camera=obj
    scene.render.engine='CYCLES'
    scene.cycles.samples=16
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1000
    scene.render.resolution_y=720
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    scene.view_settings.exposure=-.35
    (OUTPUT/'tripo-collection-assembly.json').write_text(json.dumps({'scene':scene.name,'checkpoint':str(checkpoint) if checkpoint else None,'sourceArchUnchanged':True,'models':manifest},ensure_ascii=False,indent=2),encoding='utf-8')
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CoastalTripoCollection.blend'),copy=True,compress=True)
    result={'scene':scene.name,'models':len(groups),'triangles':sum(record['triangles'] for record in manifest),'checkpoint':str(checkpoint) if checkpoint else None,'saved':str(SOURCE/'CoastalTripoCollection.blend'),'previousScene':previous.name}
finally:
    bpy.context.window.scene=previous
