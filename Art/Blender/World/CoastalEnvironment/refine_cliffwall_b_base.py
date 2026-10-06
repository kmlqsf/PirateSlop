import ast
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector,Matrix

ROOT=Path(r'D:\projects\Pirate_BR')
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
OUTPUT=ROOT/'output'/'CoastalEnvironment'
previous=bpy.context.window.scene
scene=bpy.data.scenes['TripoCoastalCollection']
tree=ast.parse((SOURCE/'assemble_tripo_collection.py').read_text(encoding='utf-8-sig'))
functions=[node for node in tree.body if isinstance(node,ast.FunctionDef) and node.name in {'fit','body'}]
exec(compile(ast.Module(body=functions,type_ignores=[]),str(SOURCE/'assemble_tripo_collection.py'),'exec'),globals())

try:
    bpy.context.window.scene=scene
    coll=bpy.data.collections['TripoSet_CliffWallB']
    old_visibility=coll.hide_viewport
    coll.hide_viewport=False
    support=coll.objects.get(coll.name+'_HeavyBaseSupport')
    if not support:
        support=body(coll,'HeavyBaseSupport','07_RockTerrace_A',(.25,.15,.55),(10.5,6.4,2.0),-3)
    bpy.context.view_layer.update()
    details=[]
    for obj in coll.objects:
        points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
        details.append({'name':obj.name,'role':obj.get('role'),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'bounds':[[min(point[i] for point in points) for i in range(3)],[max(point[i] for point in points) for i in range(3)]],'source':obj.get('source_glb',obj.get('native_plant_source'))})
    path=OUTPUT/'tripo-collection-assembly.json'
    report=json.loads(path.read_text(encoding='utf-8'))
    record=next(record for record in report['models'] if record['name']=='CliffWallB')
    record['objectsDetail']=details
    record['objects']=len(details)
    record['triangles']=sum(detail['triangles'] for detail in details)
    record['bounds']=[[min(detail['bounds'][0][i] for detail in details) for i in range(3)],[max(detail['bounds'][1][i] for detail in details) for i in range(3)]]
    record['stage']='WholeNativeTerraceBaseSupport'
    record['baseSupportSource']='07_RockTerrace_A'
    path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    coll.hide_viewport=old_visibility
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'CoastalTripoCollection.blend'),copy=True,compress=True)
    result={'name':'CliffWallB','triangles':record['triangles'],'objects':record['objects'],'support':next(detail for detail in details if detail['name']==support.name),'sourceCOLUnchanged':True,'approvedArchUnchanged':True}
finally:
    bpy.context.window.scene=previous
