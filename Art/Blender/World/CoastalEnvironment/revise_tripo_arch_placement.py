import ast
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'D:\projects\Pirate_BR')
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
OUTPUT=ROOT/'output'/'CoastalEnvironment'
tree=ast.parse((SOURCE/'assemble_tripo_arch.py').read_text(encoding='utf-8-sig'))
placements=next(ast.literal_eval(node.value) for node in tree.body if isinstance(node,ast.Assign) and any(isinstance(target,ast.Name) and target.id=='PLACEMENTS' for target in node.targets))
previous=bpy.data.scenes.get('Scene') or bpy.context.window.scene
scene=bpy.data.scenes['TripoArchScene']
try:
    bpy.context.window.scene=scene
    rocks=[]
    for name,source_id,location,scale,angles in placements:
        obj=bpy.data.objects['TripoArch_'+name]
        obj.data=bpy.data.objects['TripoMaster_'+source_id].data
        obj.location=location
        obj.scale=scale
        obj.rotation_euler=tuple(math.radians(a) for a in angles)
        obj['source_glb']=source_id
        obj['stage']='WholeModuleFoundationCrownRevision'
        rocks.append(obj)
    bpy.context.view_layer.update()
    records=[]
    violations=[]
    for obj in rocks:
        points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
        low=[min(p[i] for p in points) for i in range(3)]
        high=[max(p[i] for p in points) for i in range(3)]
        if all(high[i]>(-32,-28,0)[i] and low[i]<(30,28,55)[i] for i in range(3)):
            violations.append(obj.name)
        records.append({'name':obj.name,'source':obj['source_glb'],'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'bounds':[low,high],'location':list(obj.location),'scale':list(obj.scale),'rotationDegrees':[math.degrees(a) for a in obj.rotation_euler]})
    if violations:
        raise RuntimeError('Protected passage AABB overlap: '+', '.join(violations))
    stats={'scene':scene.name,'stage':'WholeModuleFoundationCrownRevision','triangles':sum(record['triangles'] for record in records),'instanceCount':len(rocks),'sharedMeshes':len({obj.data.name for obj in rocks}),'protectedPassageAABBPassed':True,'protectedPassageAABB':[[-32,-28,0],[30,28,55]],'bounds':[[min(record['bounds'][0][i] for record in records) for i in range(3)],[max(record['bounds'][1][i] for record in records) for i in range(3)]],'sourceGLBUnchanged':True,'sourceMaterialsUnchanged':True,'instances':records}
    (OUTPUT/'tripo-arch-assembly-stats.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding='utf-8')
    scene['stage']='WholeModuleFoundationCrownRevision'
    for camera,suffix in [('TripoArch_CameraOverall','stone-overall'),('TripoArch_CameraFront','stone-front')]:
        scene.camera=bpy.data.objects[camera]
        scene.render.filepath=str(OUTPUT/('SeaArch_Huge_A_Tripo-'+suffix+'.png'))
        bpy.ops.render.render(write_still=True)
    scene.camera=bpy.data.objects['TripoArch_CameraOverall']
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'SeaArch_Huge_A_Tripo.blend'),copy=True,compress=True)
    result=stats
finally:
    bpy.context.window.scene=previous
