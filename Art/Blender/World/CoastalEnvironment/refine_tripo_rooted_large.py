import bpy
import json
import math
import re
from datetime import datetime
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(r'D:\projects\Pirate_BR')
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
OUTPUT=ROOT/'output'/'CoastalEnvironment'
NAMES=['SeaArch_Huge_A','Sea_Lagoon_Cave','Reef_Moai_A','Reef_Spires_A','Reef_Spires_B','Reef_Spires_C','Reef_ShallowField_A']
selection=globals().get('TRIPO_REFINE_NAMES',NAMES)
previous=bpy.context.window.scene
scene=bpy.data.scenes['TripoCoastalCollection']
backup=SOURCE/('CoastalTripo_PreRootedRevision_'+datetime.now().strftime('%Y%m%d_%H%M%S')+'.blend')
protected={'SeaArch_Huge_A':[[[-32,-28,0],[30,28,55]]],'Sea_Lagoon_Cave':[[[-20,-25,0],[20,25,95]],[[-10,-140,0],[10,140,27]],[[-145,-15,0],[145,15,30]]]}

def matrix(obj):
    return obj.matrix_basis.copy() if obj.parent is None and not obj.constraints else obj.matrix_world.copy()

def bounds(obj):
    transform=matrix(obj)
    points=[transform@v.co for v in obj.data.vertices]
    return Vector(tuple(min(p[i] for p in points) for i in range(3))),Vector(tuple(max(p[i] for p in points) for i in range(3)))

def put(coll,label,source_id,center,scale,angles=(0,0,0)):
    obj=bpy.data.objects['TripoMaster_'+source_id].copy()
    obj.name=coll.name+'_'+label
    obj.parent=None
    obj.matrix_world=Matrix.Identity(4)
    obj.rotation_mode='XYZ'
    obj.rotation_euler=tuple(math.radians(value) for value in angles)
    obj.scale=(scale,scale,scale) if isinstance(scale,(int,float)) else scale
    coll.objects.link(obj)
    obj.hide_viewport=False
    obj.hide_render=False
    obj.hide_set(False)
    low,high=bounds(obj)
    obj.location+=Vector(center)-(low+high)*.5
    obj['role']='Rock'
    obj['source_glb']=source_id
    obj['model_name']=coll.name.removeprefix('TripoSet_')
    return obj

def clear_rocks(coll):
    for obj in list(coll.objects):
        if obj.type=='MESH' and obj.get('role')=='Rock':
            bpy.data.objects.remove(obj,do_unlink=True)

def roof_pair(coll,label,center,scale,yaw=0,pitch=0):
    angle=math.radians(yaw)
    direction=Vector((-math.sin(angle),math.cos(angle),0))
    offset=direction*(scale*.095)
    put(coll,label+'Front','11_RockElongated_A',Vector(center)-offset,scale,(90,pitch,yaw))
    put(coll,label+'Back','11_RockElongated_A',Vector(center)+offset,scale,(-90,pitch,yaw))

def rooted_arch(coll):
    clear_rocks(coll)
    for label,source_id,center,scale,angles in [
        ('LeftRootMass','01_RockMass_A',(-75,-2,24),60,(0,0,-4)),
        ('LeftCrown','04_RockMonolith_A',(-68,1,49),(70,65,108),(0,0,-3)),
        ('LeftInnerShoulder','05_RockMonolith_B',(-50,1,46),(39,38,98),(0,0,3)),
        ('LeftOuterFoot','09_RockWedge_A',(-90,-4,10),30,(0,0,5)),
        ('LeftForeFoot','13_RockBoulder_A',(-83,-22,4),21,(0,0,-12)),
        ('RightRootMass','03_RockMass_C',(67,0,22),57,(0,0,4)),
        ('RightCrown','04_RockMonolith_A',(66,2,34),(64,64,80),(0,0,-4)),
        ('RightInnerShoulder','05_RockMonolith_B',(48,1,39),(40,38,90),(0,0,-3)),
        ('RightOuterFoot','10_RockWedge_B',(84,-4,10),30,(0,0,-7)),
        ('RightForeFoot','13_RockBoulder_A',(79,-22,4),21,(0,0,12))
    ]:
        put(coll,label,source_id,center,scale,angles)
    roof_pair(coll,'LeftNaturalVault',(-17,1,77.5),72,0,-8)
    roof_pair(coll,'RightNaturalVault',(18,1,77.5),64,0,8)

def rooted_lagoon(coll):
    clear_rocks(coll)
    pillars=[('NW',(-67,67,39),'04_RockMonolith_A',(86,86,94),-4),('NE',(68,67,35),'05_RockMonolith_B',(72,72,84),4),('SW',(-68,-65,36),'06_RockMonolith_C',(82,82,88),-3),('SE',(67,-66,32),'04_RockMonolith_A',(72,72,78),5)]
    for index,(label,center,source_id,scale,yaw) in enumerate(pillars):
        put(coll,label+'Crown',source_id,center,scale,(0,0,yaw))
        x,y,z=center
        put(coll,label+'RootMass',['01_RockMass_A','02_RockMass_B','03_RockMass_C','01_RockMass_A'][index],(x,y+math.copysign(5,y),26),70,(0,0,0))
        put(coll,label+'OuterShoulder','09_RockWedge_A' if index%2==0 else '10_RockWedge_B',(x+math.copysign(13,x),y+math.copysign(16,y),19),54,(0,0,0))
    roof_pair(coll,'NorthNaturalVault',(-3,86,58),105,0,-2)
    roof_pair(coll,'SouthNaturalVault',(4,-84,54),98,0,2)
    roof_pair(coll,'WestNaturalVault',(-87,1,59),108,90,-2)
    roof_pair(coll,'EastNaturalVault',(86,-3,55),94,90,2)
    put(coll,'NorthUnevenCrown','03_RockMass_C',(-24,85,68),34,(0,0,-8))
    put(coll,'SouthUnevenCrown','02_RockMass_B',(22,-82,64),29,(0,0,11))

def rooted_moai(coll):
    clear_rocks(coll)
    for label,source_id,center,scale,angles in [
        ('RootedMainMass','01_RockMass_A',(0,0,.8),34,(0,0,-7)),
        ('RootedForeShoulder','09_RockWedge_A',(-11,-9,0),35,(0,0,6)),
        ('RootedSideMass','03_RockMass_C',(13,10,-1.5),27,(0,0,-9)),
        ('RootedRearFoot','13_RockBoulder_A',(-6,15,-4),23,(0,0,9))
    ]:
        put(coll,label,source_id,center,scale,angles)

def rooted_spires(coll):
    groups={}
    for obj in coll.objects:
        match=re.search(r'_Rib(?:Lower|Upper)?(\d+)$',obj.name)
        if obj.get('role')=='Rock' and match:
            groups.setdefault(int(match.group(1)),[]).append(bounds(obj))
    if not groups:
        if any(obj.get('role')=='Rock' and re.search(r'_RootedPeak\d+$',obj.name) for obj in coll.objects):
            return
        raise RuntimeError('Missing authored spire group locations: '+coll.name)
    desired=[]
    for index,boxes in sorted(groups.items()):
        low=Vector(tuple(min(box[0][i] for box in boxes) for i in range(3)))
        high=Vector(tuple(max(box[1][i] for box in boxes) for i in range(3)))
        desired.append((index,low,high))
    clear_rocks(coll)
    for index,low,high in desired:
        center=(low+high)*.5
        height=max(5,high.z+3.5)
        source_id='04_RockMonolith_A' if height>18 else ['04_RockMonolith_A','05_RockMonolith_B','06_RockMonolith_C'][index%3]
        master=bpy.data.objects['TripoMaster_'+source_id]
        native=Vector(tuple(max(v.co[i] for v in master.data.vertices)-min(v.co[i] for v in master.data.vertices) for i in range(3)))
        scale_z=height/native.z
        scale_xy=max(scale_z/2.3,(high.x-low.x)/native.x,(high.y-low.y)/native.y)
        scale_xy=min(scale_xy,scale_z*1.05)
        peak=put(coll,'RootedPeak'+str(index),source_id,(center.x,center.y,(high.z-3.5)*.5),(scale_xy,scale_xy,scale_z),(0,0,(-5,5,8)[index%3]))
        p_low,p_high=bounds(peak)
        if height>13:
            size=max(8,min(24,height*.55))
            shift=Vector(((p_high.x-p_low.x)*.19,(-1 if index%2 else 1)*(p_high.y-p_low.y)*.16,0))
            put(coll,'RootedShoulder'+str(index),'09_RockWedge_A' if index%2 else '10_RockWedge_B',(center.x+shift.x,center.y+shift.y,size*.32-3),size,(0,0,(-8,11)[index%2]))

def visible_shallow(coll):
    keep=[]
    for obj in list(coll.objects):
        if obj.get('role')!='Rock':
            continue
        if re.search(r'_(?:West|South|North|East)Header(?:\.\d+)?$',obj.name):
            bpy.data.objects.remove(obj,do_unlink=True)
            continue
        low,high=bounds(obj)
        if high.z>=0:
            keep.append(obj)
        else:
            bpy.data.objects.remove(obj,do_unlink=True)
    for label,source_id,point,scale in [('WestHeader','13_RockBoulder_A',(-16,-7,.6),9),('SouthHeader','14_RockFragment_A',(-5,-12,.4),5),('NorthHeader','15_RockFragment_B',(-3,11,.55),5),('EastHeader','13_RockBoulder_A',(16,5,.7),9)]:
        obj=put(coll,label,source_id,point,scale,(0,0,13 if label.startswith('West') else -17))
        low,high=bounds(obj)
        obj.location.z+=point[2]-high.z

def reanchor_plants(coll):
    vertices=[];faces=[]
    for obj in coll.objects:
        if obj.get('role')=='Rock':
            offset=len(vertices)
            transform=matrix(obj)
            vertices.extend(transform@v.co for v in obj.data.vertices)
            faces.extend(tuple(offset+i for i in p.vertices) for p in obj.data.polygons)
    bvh=BVHTree.FromPolygons(vertices,faces,all_triangles=False)
    for obj in coll.objects:
        if obj.get('role') not in {'Palm','Fern'}:
            continue
        low=min(v.co.z for v in obj.data.vertices)
        high=max(v.co.z for v in obj.data.vertices)
        feet=[v.co for v in obj.data.vertices if v.co.z<=low+(high-low)*.012]
        anchor=Vector((sum(v.x for v in feet)/len(feet),sum(v.y for v in feet)/len(feet),low))
        point=matrix(obj)@anchor
        found=False
        for dx,dy in [(0,0),(2,0),(-2,0),(0,2),(0,-2),(4,0),(-4,0)]:
            hit,normal,face,distance=bvh.ray_cast(Vector((point.x+dx,point.y+dy,220)),Vector((0,0,-1)),300)
            if hit is not None and normal.z>.3:
                obj.location+=hit-point-Vector((0,0,.04))
                found=True
                break
        if not found:
            raise RuntimeError('Plant has no stable rooted ledge: '+obj.name)

try:
    bpy.ops.wm.save_as_mainfile(filepath=str(backup),copy=True,compress=True)
    bpy.context.window.scene=scene
    visibility={coll.name:coll.hide_viewport for coll in scene.collection.children}
    for coll in scene.collection.children:
        coll.hide_viewport=False
    for name in selection:
        coll=bpy.data.collections['TripoSet_'+name]
        if name=='SeaArch_Huge_A':rooted_arch(coll)
        elif name=='Sea_Lagoon_Cave':rooted_lagoon(coll)
        elif name=='Reef_Moai_A':rooted_moai(coll)
        elif name=='Reef_ShallowField_A':visible_shallow(coll)
        else:rooted_spires(coll)
        reanchor_plants(coll)
    bpy.context.view_layer.update()
    report=json.loads((OUTPUT/'tripo-collection-assembly.json').read_text(encoding='utf-8'))
    for record in report['models']:
        if record['name'] not in selection:
            continue
        coll=bpy.data.collections[record['collection']]
        details=[]
        for obj in coll.objects:
            low,high=bounds(obj)
            if obj.get('role')=='Rock':
                for a,b in protected.get(record['name'],[]):
                    if all(high[i]>a[i] and low[i]<b[i] for i in range(3)):
                        raise RuntimeError('Protected opening overlap '+obj.name)
                if high.z<0:
                    raise RuntimeError('Hidden isolated underwater module '+obj.name)
            details.append({'name':obj.name,'role':obj.get('role'),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'bounds':[list(low),list(high)],'source':obj.get('source_glb',obj.get('native_plant_source'))})
        record['objectsDetail']=details
        record['objects']=len(details)
        record['triangles']=sum(detail['triangles'] for detail in details)
        record['bounds']=[[min(detail['bounds'][0][i] for detail in details) for i in range(3)],[max(detail['bounds'][1][i] for detail in details) for i in range(3)]]
        record['stage']='RootedWholeModulesNaturalSidesVault'
    report['stage']='RootedWholeModulesNaturalSidesVault'
    report['preRevisionBackup']=str(backup)
    report['collisionPolicy']='New simplified rock-only collision must match current visuals; legacy submerged debris excluded'
    (OUTPUT/'tripo-collection-assembly.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    for coll in scene.collection.children:
        if coll.name in visibility:coll.hide_viewport=visibility[coll.name]
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CoastalTripoCollection.blend'),copy=True,compress=True)
    result={'stage':report['stage'],'models':selection,'backup':str(backup),'triangles':sum(record['triangles'] for record in report['models']),'smallVisualModelsUnchanged':True,'hiddenUnderwaterModulesRemoved':True,'sourceGLBUnchanged':True}
finally:
    bpy.context.window.scene=previous
