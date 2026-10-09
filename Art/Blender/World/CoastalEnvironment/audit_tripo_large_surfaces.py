import bpy
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
names=['SeaArch_Huge_A','Sea_Lagoon_Cave','Reef_Moai_A','Reef_Spires_A','Reef_Spires_B','Reef_Spires_C','Reef_ShallowField_A']
records=[]
for name in names:
    coll=bpy.data.collections['TripoSet_'+name]
    modules=[]
    for obj in coll.objects:
        if obj.type!='MESH' or obj.get('role')!='Rock':
            continue
        matrix=obj.matrix_basis.copy() if obj.parent is None and not obj.constraints else obj.matrix_world.copy()
        vertices=[matrix@v.co for v in obj.data.vertices]
        faces=[tuple(p.vertices) for p in obj.data.polygons]
        low=Vector(tuple(min(v[i] for v in vertices) for i in range(3)))
        high=Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
        modules.append({'object':obj,'matrix':matrix,'vertices':vertices,'faces':faces,'low':low,'high':high,'bvh':BVHTree.FromPolygons(vertices,faces,all_triangles=False)})
    details=[]
    for module in modules:
        obj=module['object']
        low=module['low'];high=module['high']
        downward_area=0
        exposed_area=0
        exposed_faces=0
        largest_triangle=0
        centroid_sum=Vector((0,0,0))
        for face in module['faces']:
            for index in range(1,len(face)-1):
                a,b,c=(module['vertices'][i] for i in (face[0],face[index],face[index+1]))
                cross=(b-a).cross(c-a)
                area=cross.length*.5
                if area<1e-9:
                    continue
                normal=cross.normalized()
                point=(a+b+c)/3
                if normal.z>-.92 or point.z>low.z+(high.z-low.z)*.25:
                    continue
                downward_area+=area
                query=point+normal*.01
                hidden=False
                for other in modules:
                    if other is module or any(query[i]<other['low'][i] or query[i]>other['high'][i] for i in range(3)):
                        continue
                    closest,other_normal,face_index,distance=other['bvh'].find_nearest(query)
                    if closest is not None and (query-closest).dot(other_normal)<-.001:
                        hidden=True
                        break
                if not hidden:
                    exposed_area+=area
                    exposed_faces+=1
                    largest_triangle=max(largest_triangle,area)
                    centroid_sum+=point*area
        scales=[abs(value) for value in module['matrix'].to_scale()]
        details.append({'name':obj.name,'source':obj.get('source_glb'),'bounds':[list(low),list(high)],'location':list(obj.location),'scale':list(obj.scale),'rotationDegrees':[math.degrees(value) for value in obj.rotation_euler],'rotationMode':obj.rotation_mode,'matrixWorld':[list(row) for row in module['matrix']],'nonUniformScaleRatio':max(scales)/max(1e-9,min(scales)),'fullySubmerged':high.z<0,'downwardNearBaseArea':downward_area,'estimatedExposedDownwardArea':exposed_area,'estimatedExposedFaces':exposed_faces,'largestExposedDownwardTriangle':largest_triangle,'exposedCentroid':list(centroid_sum/exposed_area) if exposed_area else None})
    records.append({'name':name,'modules':details,'estimatedExposedDownwardArea':sum(detail['estimatedExposedDownwardArea'] for detail in details)})
path=OUTPUT/'tripo-large-native-surface-audit.json'
path.write_text(json.dumps({'method':'Actual world triangles; near-base downward faces; neighbour containment estimated by signed closest surface, not a visual verdict','sourceSceneUnchanged':True,'models':records},ensure_ascii=False,indent=2),encoding='utf-8')
result={'path':str(path),'models':len(records),'sourceSceneUnchanged':True,'activeScene':bpy.context.window.scene.name}
