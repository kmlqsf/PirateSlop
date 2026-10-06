import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

previous=bpy.data.scenes.get('Scene') or bpy.context.window.scene
scene=bpy.data.scenes['TripoArchScene']
OUTPUT=Path(r'D:\projects\Pirate_BR\output\CoastalEnvironment')
try:
    bpy.context.window.scene=scene
    for obj in list(scene.objects):
        if obj.name.startswith('TripoArch_Toe_'):
            bpy.data.objects.remove(obj,do_unlink=True)
    records=[]
    for index,(source_id,x,y,width,depth,height,yaw) in enumerate([
        ('14_RockFragment_A',-102,-24,6,4.2,3.1,21),
        ('15_RockFragment_B',-92,-33,7.5,4.8,3.7,-17),
        ('14_RockFragment_A',-77,-33,8.2,5,4.1,-36),
        ('15_RockFragment_B',-52,-28,6.8,4.1,3.2,36),
        ('14_RockFragment_A',96,-26,6.2,4.4,3,13),
        ('15_RockFragment_B',80,-30,7.6,5,3.6,-24),
        ('14_RockFragment_A',50,-29,8,5.5,3.8,38),
        ('15_RockFragment_B',89,-17,5.8,4.1,2.6,61)
    ]):
        source=bpy.data.objects['TripoMaster_'+source_id]
        low=Vector(tuple(min(vertex.co[i] for vertex in source.data.vertices) for i in range(3)))
        high=Vector(tuple(max(vertex.co[i] for vertex in source.data.vertices) for i in range(3)))
        dimensions=high-low
        obj=source.copy()
        obj.name='TripoArch_Toe_'+str(index)
        scene.collection.objects.link(obj)
        obj.hide_render=False
        obj.hide_set(False)
        obj.scale=(width/dimensions.x,depth/dimensions.y,height/dimensions.z)
        obj.rotation_mode='XYZ'
        obj.rotation_euler=(0,0,math.radians(yaw))
        obj.location=(x,y,-low.z*obj.scale.z-.22)
        obj['source_glb']=source_id
        obj['stage']='GroundingFragments'
        bpy.context.view_layer.update()
        points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
        minimum=[min(point[i] for point in points) for i in range(3)]
        maximum=[max(point[i] for point in points) for i in range(3)]
        if all(maximum[i]>(-32,-28,0)[i] and minimum[i]<(30,28,55)[i] for i in range(3)):
            raise RuntimeError('Foot stone enters protected passage: '+obj.name)
        records.append({'name':obj.name,'source':source_id,'triangles':sum(len(poly.vertices)-2 for poly in obj.data.polygons),'bounds':[minimum,maximum]})
    stones=[obj for obj in scene.objects if obj.type=='MESH' and 'source_glb' in obj]
    report={'footstones':records,'rockInstances':len(stones),'rockTriangles':sum(sum(len(poly.vertices)-2 for poly in obj.data.polygons) for obj in stones),'nativeMeshAndMaterialsShared':True,'protectedPassagePassed':True}
    (OUTPUT/'tripo-arch-footstones-stats.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    result=report
finally:
    bpy.context.window.scene=previous
