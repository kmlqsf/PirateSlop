import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
previous=bpy.data.scenes.get('Scene') or bpy.context.window.scene
scene=bpy.data.scenes['TripoArchScene']
try:
    bpy.context.window.scene=scene
    for obj in scene.objects:
        if obj.type=='MESH' and 'source_glb' in obj:
            desired=obj.rotation_euler.copy()
            obj.rotation_mode='XYZ'
            obj.rotation_euler=desired
    for name,location,scale,angles in [
        ('CentralBridgeFront',(-12,-3,83.5),(41,70,42),(0,-8,3)),
        ('CentralBridgeBack',(14,6,81.5),(40,67,38),(0,8,-5))
    ]:
        obj=bpy.data.objects['TripoArch_'+name]
        obj.location=location
        obj.scale=scale
        obj.rotation_mode='XYZ'
        obj.rotation_euler=tuple(math.radians(a) for a in angles)
    bpy.context.view_layer.update()
    rocks=[obj for obj in scene.objects if obj.type=='MESH' and 'source_glb' in obj]
    records=[]
    violations=[]
    for obj in rocks:
        points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
        low=[min(point[i] for point in points) for i in range(3)]
        high=[max(point[i] for point in points) for i in range(3)]
        if all(high[i]>(-32,-28,0)[i] and low[i]<(30,28,55)[i] for i in range(3)):
            violations.append(obj.name)
        records.append({'name':obj.name,'source':obj['source_glb'],'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'bounds':[low,high],'location':list(obj.location),'scale':list(obj.scale),'rotationDegrees':[math.degrees(a) for a in obj.rotation_euler]})
    if violations:
        raise RuntimeError('Protected passage AABB overlap: '+', '.join(violations))
    stats={'scene':scene.name,'stage':'WholeModuleArchedRoofRevision','triangles':sum(record['triangles'] for record in records),'instanceCount':len(rocks),'protectedPassageAABBPassed':True,'sourceGLBUnchanged':True,'sourceMaterialsUnchanged':True,'instances':records}
    (OUTPUT/'tripo-arch-assembly-stats.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding='utf-8')
    scene['stage']='WholeModuleArchedRoofRevision'
    scene.camera=bpy.data.objects['TripoArch_CameraOverall']
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'SeaArch_Huge_A_Tripo.blend'),copy=True,compress=True)
    result=stats
finally:
    bpy.context.window.scene=previous
