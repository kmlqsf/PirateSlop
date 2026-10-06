import ast
import bpy
import json
from pathlib import Path
from mathutils import Vector,Matrix
import math

ROOT=Path(r'D:\projects\Pirate_BR')
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
OUTPUT=ROOT/'output'/'CoastalEnvironment'
previous=bpy.context.window.scene
scene=bpy.data.scenes['TripoCoastalCollection']
components={record['name']:record['components'] for record in json.loads((OUTPUT/'physical-component-bounds.json').read_text(encoding='utf-8-sig'))}
tree=ast.parse((SOURCE/'assemble_tripo_collection.py').read_text(encoding='utf-8-sig'))
functions=[node for node in tree.body if isinstance(node,ast.FunctionDef) and node.name in {'fit','body','foot'}]
exec(compile(ast.Module(body=functions,type_ignores=[]),str(SOURCE/'assemble_tripo_collection.py'),'exec'),globals())
UPPER=[
    ('NorthBridgeLeft','11_RockElongated_A',(-30,78,57),(60,45,40),-4),
    ('NorthBridgeMiddle','02_RockMass_B',(5,88,61),(58,48,40),5),
    ('NorthBridgeRight','08_RockTerrace_B',(45,77,58),(60,47,43),-6),
    ('SouthBridgeLeft','12_RockElongated_B',(-32,-76,60),(62,47,43),5),
    ('SouthBridgeMiddle','03_RockMass_C',(6,-88,61),(62,50,42),-4),
    ('SouthBridgeRight','07_RockTerrace_A',(44,-75,60),(61,47,43),8),
    ('WestBridgeNorth','01_RockMass_A',(-87,48,59),(55,62,46),-3),
    ('WestBridgeMiddle','05_RockMonolith_B',(-96,8,62),(55,70,45),3),
    ('WestBridgeSouth','09_RockWedge_A',(-89,-35,59),(57,64,46),7),
    ('EastBridgeSouth','02_RockMass_B',(84,-40,57),(53,64,47),-5),
    ('EastBridgeMiddle','03_RockMass_C',(96,2,63),(54,67,43),-3),
    ('EastBridgeNorth','10_RockWedge_B',(85,44,57),(53,62,46),6)
]

def bounds(coll):
    bpy.context.view_layer.update()
    points=[obj.matrix_world@Vector(corner) for obj in coll.objects if obj.type=='MESH' and obj.get('role')=='Rock' for corner in obj.bound_box]
    return [min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]

try:
    bpy.context.window.scene=scene
    visibility={coll.name:coll.hide_viewport for coll in scene.collection.children}
    for coll in scene.collection.children:
        coll.hide_viewport=False
    lagoon=bpy.data.collections['TripoSet_Sea_Lagoon_Cave']
    for label,source_id,center,size,yaw in UPPER:
        old=lagoon.objects.get(lagoon.name+'_'+label)
        if old:
            bpy.data.objects.remove(old,do_unlink=True)
        body(lagoon,label,source_id,center,size,yaw)
    for name in ['RockLarge','CliffWallA','CliffWallB','CliffWallC']:
        coll=bpy.data.collections['TripoSet_'+name]
        low,high=bounds(coll)
        delta=-.15-low[2]
        for obj in coll.objects:
            obj.location.z+=delta
    ridge=bpy.data.collections['TripoSet_Reef_Spires_C']
    candidates=[c for c in components['Reef_Spires_C'] if c['high'][0]<-80 and c['high'][2]>2]
    if candidates:
        comp=max(candidates,key=lambda c:c['high'][2])
        low=list(comp['low']);high=comp['high']
        low[2]=max(-3.5,low[2])
        if not ridge.objects.get(ridge.name+'_LeftOuterPhysicalRock'):
            body(ridge,'LeftOuterPhysicalRock','14_RockFragment_A',[(low[i]+high[i])*.5 for i in range(3)],[high[i]-low[i] for i in range(3)],9)
    edge=[c for c in components['Reef_Spires_C'] if -3.6<c['high'][2]<0 and min(c['high'][i]-c['low'][i] for i in (0,1))>3]
    for label,source_id,comp in [('LeftPhysicalTerrace','07_RockTerrace_A',min(edge,key=lambda c:c['low'][0])),('RightPhysicalTerrace','08_RockTerrace_B',max(edge,key=lambda c:c['high'][0]))]:
        if not ridge.objects.get(ridge.name+'_'+label):
            low=comp['low'];high=comp['high']
            body(ridge,label,source_id,[(low[i]+high[i])*.5 for i in range(3)],[high[i]-low[i] for i in range(3)],0)
    bpy.context.view_layer.update()
    protected={'SeaArch_Huge_A':[[[-32,-28,0],[30,28,55]]],'Sea_Lagoon_Cave':[[[-20,-25,0],[20,25,95]],[[-10,-140,0],[10,140,27]],[[-145,-15,0],[145,15,30]]]}
    report=json.loads((OUTPUT/'tripo-collection-assembly.json').read_text(encoding='utf-8'))
    models=[]
    for record in report['models']:
        name=record['name']
        coll=bpy.data.collections[record['collection']]
        details=[]
        for obj in coll.objects:
            points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
            low=[min(p[i] for p in points) for i in range(3)]
            high=[max(p[i] for p in points) for i in range(3)]
            if obj.get('role')=='Rock':
                for a,b in protected.get(name,[]):
                    if all(high[i]>a[i] and low[i]<b[i] for i in range(3)):
                        raise RuntimeError(name+' protected passage overlap '+obj.name)
            details.append({'name':obj.name,'role':obj.get('role'),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'bounds':[low,high],'source':obj.get('source_glb',obj.get('native_plant_source'))})
        models.append({'name':name,'collection':coll.name,'triangles':sum(detail['triangles'] for detail in details),'objects':len(details),'bounds':[[min(detail['bounds'][0][i] for detail in details) for i in range(3)],[max(detail['bounds'][1][i] for detail in details) for i in range(3)]],'protectedPassages':protected.get(name,[]),'protectedPassagesPassed':True,'objectsDetail':details})
    report['models']=models
    report['stage']='WholeModuleShouldersAndFootCoverage'
    (OUTPUT/'tripo-collection-assembly.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    for coll in scene.collection.children:
        if coll.name in visibility:
            coll.hide_viewport=visibility[coll.name]
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CoastalTripoCollection.blend'),copy=True,compress=True)
    result={'stage':report['stage'],'models':len(models),'triangles':sum(record['triangles'] for record in models),'protectedPassagesPassed':True,'approvedArchUnchanged':True}
finally:
    bpy.context.window.scene=previous
