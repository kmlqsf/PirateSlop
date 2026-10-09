import bpy
import json
import math
from pathlib import Path
from datetime import datetime
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree

ROOT=Path(r'D:\projects\Pirate_BR')
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
OUTPUT=ROOT/'output'/'CoastalEnvironment'
previous=bpy.context.window.scene
scene=bpy.data.scenes['TripoCoastalCollection']
coll=bpy.data.collections['TripoSet_Sea_Lagoon_Cave']
boxes=[((-20,-25,0),(20,25,95)),((-10,-140,0),(10,140,27)),((-145,-15,0),(145,15,30))]
placements=[
    ('NorthWest','01_RockMass_A',(-44,86,31),(64,64,74),'NW','North'),
    ('NorthEast','02_RockMass_B',(44,86,30),(64,64,72),'NE','North'),
    ('SouthWest','03_RockMass_C',(-44,-84,29),(64,64,72),'SW','South'),
    ('SouthEast','01_RockMass_A',(44,-84,28),(64,64,68),'SE','South'),
    ('WestNorth','02_RockMass_B',(-87,44,31),(64,60,74),'NW','West'),
    ('WestSouth','03_RockMass_C',(-87,-44,30),(64,64,74),'SW','West'),
    ('EastNorth','01_RockMass_A',(86,44,29),(64,60,72),'NE','East'),
    ('EastSouth','02_RockMass_B',(86,-44,28),(62,60,70),'SE','East')
]

def transform(obj):
    return obj.matrix_basis.copy() if obj.parent is None and not obj.constraints else obj.matrix_world.copy()

def bounds(obj):
    points=[transform(obj)@vertex.co for vertex in obj.data.vertices]
    return Vector(tuple(min(point[i] for point in points) for i in range(3))),Vector(tuple(max(point[i] for point in points) for i in range(3)))

def bvh(obj):
    points=[transform(obj)@vertex.co for vertex in obj.data.vertices]
    return BVHTree.FromPolygons(points,[tuple(face.vertices) for face in obj.data.polygons],all_triangles=False)

def contacts(obj,others):
    tree=bvh(obj)
    return [{'other':other.name,'triangleIntersections':len(tree.overlap(bvh(other)))} for other in others]

try:
    backup=SOURCE/('CoastalTripo_PreShoulders_'+datetime.now().strftime('%Y%m%d_%H%M%S')+'.blend')
    bpy.ops.wm.save_as_mainfile(filepath=str(backup),copy=True,compress=True)
    bpy.context.window.scene=scene
    for obj in list(coll.objects):
        if '_RootedVaultShoulder_' in obj.name:
            bpy.data.objects.remove(obj,do_unlink=True)
    connectivity=[]
    for label,source_id,center,scale,corner,vault in placements:
        obj=bpy.data.objects['TripoMaster_'+source_id].copy()
        obj.name=coll.name+'_RootedVaultShoulder_'+label
        obj.parent=None
        obj.matrix_world=Matrix.Identity(4)
        obj.rotation_mode='XYZ'
        obj.rotation_euler=(0,0,0)
        obj.scale=scale
        coll.objects.link(obj)
        obj.hide_viewport=False
        obj.hide_render=False
        obj.hide_set(False)
        low,high=bounds(obj)
        obj.location+=Vector(center)-(low+high)*.5
        obj['role']='Rock'
        obj['source_glb']=source_id
        obj['model_name']='Sea_Lagoon_Cave'
        low,high=bounds(obj)
        for a,b in boxes:
            if all(high[i]>a[i] and low[i]<b[i] for i in range(3)):
                raise RuntimeError('Rooted shoulder intrudes into protected passage: '+label)
        if low.z>=0:
            raise RuntimeError('Shoulder is not rooted below water: '+label)
        corner_objects=[other for other in coll.objects if other.get('role')=='Rock' and other.name in {coll.name+'_'+corner+'Crown',coll.name+'_'+corner+'RootMass'}]
        vault_objects=[other for other in coll.objects if other.get('role')=='Rock' and other.name in {coll.name+'_'+vault+'NaturalVaultFront',coll.name+'_'+vault+'NaturalVaultBack'}]
        corner_contacts=contacts(obj,corner_objects)
        vault_contacts=contacts(obj,vault_objects)
        connectivity.append({'shoulder':obj.name,'bounds':[list(low),list(high)],'cornerContacts':corner_contacts,'vaultContacts':vault_contacts,'hasActualCornerSurfaceIntersection':any(record['triangleIntersections']>0 for record in corner_contacts),'hasActualVaultSurfaceIntersection':any(record['triangleIntersections']>0 for record in vault_contacts)})
    if any(not item['hasActualCornerSurfaceIntersection'] or not item['hasActualVaultSurfaceIntersection'] for item in connectivity):
        raise RuntimeError('Disconnected rooted shoulder: '+str(connectivity))
    vertices=[]
    faces=[]
    for obj in coll.objects:
        if obj.get('role')!='Rock':continue
        offset=len(vertices)
        vertices.extend(transform(obj)@vertex.co for vertex in obj.data.vertices)
        faces.extend(tuple(offset+index for index in face.vertices) for face in obj.data.polygons)
    tree=BVHTree.FromPolygons(vertices,faces,all_triangles=False)
    for obj in coll.objects:
        if obj.get('role') not in {'Palm','Fern'}:continue
        zlow=min(vertex.co.z for vertex in obj.data.vertices)
        zhigh=max(vertex.co.z for vertex in obj.data.vertices)
        feet=[vertex.co for vertex in obj.data.vertices if vertex.co.z<=zlow+(zhigh-zlow)*.012]
        local=Vector((sum(point.x for point in feet)/len(feet),sum(point.y for point in feet)/len(feet),zlow))
        anchor=transform(obj)@local
        for dx,dy in [(0,0),(2,0),(-2,0),(0,2),(0,-2),(4,0),(-4,0)]:
            hit,normal,face,distance=tree.ray_cast(Vector((anchor.x+dx,anchor.y+dy,220)),Vector((0,0,-1)),300)
            if hit is not None and normal.z>.3:
                obj.location+=hit-anchor-Vector((0,0,.04))
                break
        else:
            raise RuntimeError('Plant has no stable ledge after shoulder connection: '+obj.name)
    report_path=OUTPUT/'tripo-collection-assembly.json'
    report=json.loads(report_path.read_text(encoding='utf-8'))
    record=next(record for record in report['models'] if record['name']=='Sea_Lagoon_Cave')
    details=[]
    for obj in coll.objects:
        low,high=bounds(obj)
        details.append({'name':obj.name,'role':obj.get('role'),'triangles':sum(len(face.vertices)-2 for face in obj.data.polygons),'bounds':[list(low),list(high)],'source':obj.get('source_glb',obj.get('native_plant_source'))})
    record['objectsDetail']=details
    record['objects']=len(details)
    record['triangles']=sum(detail['triangles'] for detail in details)
    record['bounds']=[[min(detail['bounds'][0][i] for detail in details) for i in range(3)],[max(detail['bounds'][1][i] for detail in details) for i in range(3)]]
    record['stage']='RootedVaultShouldersActualSurfaceConnections'
    record['shoulderConnectivity']=connectivity
    report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CoastalTripoCollection.blend'),copy=True,compress=True)
    result={'stage':record['stage'],'triangles':record['triangles'],'connectivity':connectivity,'backup':str(backup),'nativeMastersUnchanged':True,'otherModelsUnchanged':True}
finally:
    bpy.context.window.scene=previous
