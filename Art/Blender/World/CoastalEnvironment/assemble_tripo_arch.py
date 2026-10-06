import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
previous=bpy.data.scenes.get('Scene') or bpy.context.window.scene
scene=bpy.data.scenes.get('TripoArchScene') or bpy.data.scenes.new('TripoArchScene')
PLACEMENTS=[
    ('LeftBase','01_RockMass_A',(-74,-6,23),(60,52,60),(0,0,-3)),
    ('LeftUpperMass','02_RockMass_B',(-67,0,68),(50,50,70),(0,0,5)),
    ('LeftCrownRib','04_RockMonolith_A',(-59,-5,71),(42,50,64),(0,-2,-4)),
    ('LeftOuterRib','05_RockMonolith_B',(-85,-7,48),(35,31,47),(0,3,7)),
    ('LeftFrontRib','06_RockMonolith_C',(-72,-16,49),(33,38,53),(0,-3,-8)),
    ('LeftTerrace','07_RockTerrace_A',(-86,-15,25),(35,31,30),(0,0,11)),
    ('LeftOuterFoot','09_RockWedge_A',(-85,-22,10),(37,26,34),(0,0,-5)),
    ('RightBase','03_RockMass_C',(65,-5,18),(57,50,50),(0,0,2)),
    ('RightUpperMass','02_RockMass_B',(61,7,51),(45,42,48),(0,0,-6)),
    ('RightCrownRib','04_RockMonolith_A',(52,-8,52),(40,46,60),(0,2,5)),
    ('RightOuterRib','05_RockMonolith_B',(77,-15,36),(25,24,49),(0,-3,-10)),
    ('RightTerrace','08_RockTerrace_B',(76,-14,25),(33,27,29),(0,0,-8)),
    ('RightOuterFoot','10_RockWedge_B',(85,-16,8),(25,30,29),(0,0,5)),
    ('RightFrontFoot','13_RockBoulder_A',(63,-24,5),(27,27,25),(0,0,18)),
    ('LeftBridgeMass','01_RockMass_A',(-42,2,78),(50,45,37),(0,3,-8)),
    ('CentralBridgeFront','11_RockElongated_A',(-12,-3,83.5),(41,70,42),(0,-8,3)),
    ('CentralBridgeBack','12_RockElongated_B',(14,6,81.5),(40,67,38),(0,8,-5)),
    ('RightBridgeMass','03_RockMass_C',(35,3,73),(43,42,31),(0,-3,5)),
    ('LeftInnerShoulder','09_RockWedge_A',(-41,1,67),(27,36,26),(0,0,-5)),
    ('RightInnerShoulder','10_RockWedge_B',(38,1,67),(25,34,25),(0,0,6)),
    ('LeftFootFragment','14_RockFragment_A',(-98,-30,2),(9,11,10),(0,0,28)),
    ('RightFootFragment','15_RockFragment_B',(93,-23,1),(10,11,8),(0,0,-25)),
    ('LeftShoulderFragment','15_RockFragment_B',(-85,-21,36),(11,11,12),(0,0,11)),
    ('RightShoulderFragment','14_RockFragment_A',(81,-21,39),(10,11,9),(0,0,-15))
]

def bounds(obj):
    points=[obj.matrix_world@Vector(corner) for corner in obj.bound_box]
    return [min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]

def camera(name,target,position,scale):
    data=bpy.data.cameras.new(name)
    data.type='ORTHO'
    data.ortho_scale=scale
    obj=bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location=position
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    return obj

def light(name,position,energy,size,target):
    data=bpy.data.lights.new(name,'AREA')
    data.energy=energy
    data.shape='DISK'
    data.size=size
    obj=bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location=position
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()

try:
    bpy.context.window.scene=scene
    for obj in list(scene.objects):
        if obj.name.startswith('TripoArch_'):
            bpy.data.objects.remove(obj,do_unlink=True)
    rocks=[]
    for name,source_id,location,scale,angles in PLACEMENTS:
        source=bpy.data.objects['TripoMaster_'+source_id]
        obj=source.copy()
        obj.name='TripoArch_'+name
        scene.collection.objects.link(obj)
        obj.hide_render=False
        obj.hide_set(False)
        obj.location=location
        obj.scale=scale
        obj.rotation_mode='XYZ'
        obj.rotation_euler=tuple(math.radians(a) for a in angles)
        obj['source_glb']=source_id
        obj['stage']='StoneComposition'
        rocks.append(obj)
    bpy.context.view_layer.update()
    protected_low=(-32,-28,0)
    protected_high=(30,28,55)
    records=[]
    violations=[]
    for obj in rocks:
        low,high=bounds(obj)
        overlap=all(high[i]>protected_low[i] and low[i]<protected_high[i] for i in range(3))
        if overlap:
            violations.append(obj.name)
        records.append({'name':obj.name,'source':obj['source_glb'],'triangles':sum(len(poly.vertices)-2 for poly in obj.data.polygons),'bounds':[low,high],'location':list(obj.location),'scale':list(obj.scale),'rotationDegrees':[math.degrees(a) for a in obj.rotation_euler]})
    if violations:
        raise RuntimeError('Protected passage AABB overlap: '+', '.join(violations))
    world=bpy.data.worlds.new('TripoArch_NeutralWorld')
    world.use_nodes=True
    background=next(node for node in world.node_tree.nodes if node.type=='BACKGROUND')
    background.inputs['Color'].default_value=(.43,.48,.55,1)
    background.inputs['Strength'].default_value=.32
    scene.world=world
    light('TripoArch_Key',(-120,-150,230),520000,110,(-15,0,45))
    light('TripoArch_Fill',(155,-80,130),190000,140,(15,0,45))
    light('TripoArch_Rim',(-40,170,190),380000,100,(0,0,55))
    material=bpy.data.materials.new('TripoArch_WaterlineGround')
    material.use_nodes=True
    shader=next(node for node in material.node_tree.nodes if node.type=='BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value=(.19,.235,.26,1)
    shader.inputs['Roughness'].default_value=.84
    mesh=bpy.data.meshes.new('TripoArch_Ground')
    mesh.from_pydata([(-700,-700,0),(700,-700,0),(700,700,0),(-700,700,0)],[],[(0,1,2,3)])
    ground=bpy.data.objects.new('TripoArch_Ground',mesh)
    ground.data.materials.append(material)
    scene.collection.objects.link(ground)
    overall=camera('TripoArch_CameraOverall',(0,0,47),(112,-355,145),236)
    front=camera('TripoArch_CameraFront',(0,0,48),(0,-355,125),230)
    detail=camera('TripoArch_CameraDetail',(-48,-13,69),(24,-160,118),92)
    side=camera('TripoArch_CameraPassage',(0,4,57),(80,-270,106),200)
    scene.camera=overall
    scene.render.engine='CYCLES'
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.render.resolution_x=1600
    scene.render.resolution_y=1100
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'
    scene.view_settings.exposure=-.35
    scene['assembly_source']='assemble_tripo_arch.py'
    scene['stage']='StoneComposition'
    scene['protected_passage_json']=json.dumps([protected_low,protected_high])
    OUTPUT.mkdir(parents=True,exist_ok=True)
    manifest={'scene':scene.name,'stage':'StoneComposition','sourceGLBUnchanged':True,'sourceMaterialsUnchanged':True,'triangles':sum(record['triangles'] for record in records),'instanceCount':len(rocks),'sharedMeshes':len({obj.data.name for obj in rocks}),'protectedPassageAABB':[protected_low,protected_high],'protectedPassageAABBPassed':True,'bounds':[[min(record['bounds'][0][i] for record in records) for i in range(3)],[max(record['bounds'][1][i] for record in records) for i in range(3)]],'instances':records}
    (OUTPUT/'tripo-arch-assembly-stats.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    scene.camera=overall
    scene.render.filepath=str(OUTPUT/'SeaArch_Huge_A_Tripo-stone-overall.png')
    bpy.ops.render.render(write_still=True)
    scene.camera=front
    scene.render.filepath=str(OUTPUT/'SeaArch_Huge_A_Tripo-stone-front.png')
    bpy.ops.render.render(write_still=True)
    scene.camera=overall
    bpy.ops.object.select_all(action='DESELECT')
    for obj in rocks:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=rocks[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'SeaArch_Huge_A_Tripo.blend'),copy=True,compress=True)
    result={'scene':scene.name,'triangles':manifest['triangles'],'instances':len(rocks),'protectedPassage':True,'overall':str(OUTPUT/'SeaArch_Huge_A_Tripo-stone-overall.png'),'front':str(OUTPUT/'SeaArch_Huge_A_Tripo-stone-front.png'),'blend':str(SOURCE/'SeaArch_Huge_A_Tripo.blend'),'originalSceneRestored':previous.name}
finally:
    bpy.context.window.scene=previous
