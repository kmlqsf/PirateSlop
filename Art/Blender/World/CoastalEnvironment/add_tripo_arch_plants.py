import bpy
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path(r'D:\projects\Pirate_BR')
OUTPUT=ROOT/'output'/'CoastalEnvironment'
SOURCE=ROOT/'PirateGame'/'Art'/'Blender'/'World'/'CoastalEnvironment'
BITMAPS=ROOT/'PirateGame'/'Art'/'Sources'/'CoastalEnvironment'
previous=bpy.data.scenes.get('Scene') or bpy.context.window.scene
scene=bpy.data.scenes['TripoArchScene']

def material(name,diffuse,normal,alpha=None):
    mat=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    links=mat.node_tree.links
    nodes.clear()
    output=nodes.new('ShaderNodeOutputMaterial')
    shader=nodes.new('ShaderNodeBsdfPrincipled')
    shader.inputs['Roughness'].default_value=.78
    shader.inputs['Specular IOR Level'].default_value=.22
    links.new(shader.outputs['BSDF'],output.inputs['Surface'])
    image=nodes.new('ShaderNodeTexImage')
    image.image=bpy.data.images.load(str(diffuse),check_existing=True)
    image.image.colorspace_settings.name='sRGB'
    image.image.pack()
    separate=nodes.new('ShaderNodeSeparateColor')
    links.new(image.outputs['Color'],separate.inputs['Color'])
    red=nodes.new('ShaderNodeMath')
    red.operation='MULTIPLY'
    red.inputs[1].default_value=.95
    links.new(separate.outputs['Red'],red.inputs[0])
    green=nodes.new('ShaderNodeMath')
    green.operation='GREATER_THAN'
    links.new(separate.outputs['Green'],green.inputs[0])
    links.new(red.outputs[0],green.inputs[1])
    gain=nodes.new('ShaderNodeMixRGB')
    gain.blend_type='MULTIPLY'
    gain.inputs[0].default_value=1
    gain.inputs[2].default_value=(1.35,1.9,1.2,1)
    links.new(image.outputs['Color'],gain.inputs[1])
    mix=nodes.new('ShaderNodeMixRGB')
    links.new(green.outputs[0],mix.inputs[0])
    links.new(image.outputs['Color'],mix.inputs[1])
    links.new(gain.outputs[0],mix.inputs[2])
    links.new(mix.outputs['Color'],shader.inputs['Base Color'])
    if alpha:
        mask=nodes.new('ShaderNodeTexImage')
        mask.image=bpy.data.images.load(str(alpha),check_existing=True)
        mask.image.colorspace_settings.name='Non-Color'
        mask.image.pack()
        links.new(mask.outputs['Color'],shader.inputs['Alpha'])
    else:
        links.new(image.outputs['Alpha'],shader.inputs['Alpha'])
    normal_image=nodes.new('ShaderNodeTexImage')
    normal_image.image=bpy.data.images.load(str(normal),check_existing=True)
    normal_image.image.colorspace_settings.name='Non-Color'
    normal_image.image.pack()
    normal_node=nodes.new('ShaderNodeNormalMap')
    normal_node.inputs['Strength'].default_value=.8
    links.new(normal_image.outputs['Color'],normal_node.inputs['Color'])
    links.new(normal_node.outputs['Normal'],shader.inputs['Normal'])
    mat['NativeUVBitmapPreserved']=True
    mat['LeafOnlyRGBGain']=[1.35,1.9,1.2]
    return mat

try:
    bpy.context.window.scene=scene
    for obj in list(scene.objects):
        if obj.name.startswith('TripoArch_Plant_'):
            bpy.data.objects.remove(obj,do_unlink=True)
    rocks=[obj for obj in scene.objects if obj.type=='MESH' and 'source_glb' in obj]
    vertices=[]
    polygons=[]
    bpy.context.view_layer.update()
    for obj in rocks:
        offset=len(vertices)
        vertices.extend(obj.matrix_world@vertex.co for vertex in obj.data.vertices)
        polygons.extend(tuple(offset+i for i in polygon.vertices) for polygon in obj.data.polygons)
    bvh=BVHTree.FromPolygons(vertices,polygons,all_triangles=False)
    palm_mat=material('TripoArch_NativePalm',BITMAPS/'yughues_palms'/'diffuse.tga',BITMAPS/'yughues_palms'/'normal.tga')
    fern_mat=material('TripoArch_NativeFern',BITMAPS/'fern_02'/'textures'/'fern_02_diff_2k.png',BITMAPS/'fern_02'/'textures'/'fern_02_nor_gl_2k.png',BITMAPS/'fern_02'/'textures'/'fern_02_alpha_2k.png')
    source_fern=bpy.data.objects['CC0Master_Fern']
    fern_mesh=bpy.data.meshes.get('TripoArch_PreparedFernMesh')
    if fern_mesh is None:
        fern_mesh=source_fern.data.copy()
        fern_mesh.name='TripoArch_PreparedFernMesh'
        temporary=bpy.data.objects.new('TripoArch_PreparedFernTemporary',fern_mesh)
        scene.collection.objects.link(temporary)
        bpy.ops.object.select_all(action='DESELECT')
        temporary.select_set(True)
        bpy.context.view_layer.objects.active=temporary
        triangles=sum(len(p.vertices)-2 for p in fern_mesh.polygons)
        modifier=temporary.modifiers.new('KeepNativeLeafUV','DECIMATE')
        modifier.ratio=min(1,900/triangles)
        modifier.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        fern_mesh=temporary.data
        bpy.data.objects.remove(temporary,do_unlink=True)
    supports=[]
    plants=[]
    def place(name,key,x,y,height,yaw):
        hit=None
        for dx,dy in [(0,0),(-2,0),(2,0),(0,-2),(0,2),(-2,-2),(2,2),(-4,0),(4,0),(0,-4),(0,4)]:
            point,normal,index,distance=bvh.ray_cast(Vector((x+dx,y+dy,160)),Vector((0,0,-1)),200)
            if point is not None and normal.z>=(.45 if key!='Fern' else .30):
                hit=(point,normal)
                break
        if hit is None:
            raise RuntimeError('Plant support not found: '+name)
        point,normal=hit
        source=bpy.data.objects['CC0Master_'+key]
        obj=source.copy()
        obj.data=fern_mesh if key=='Fern' else source.data
        obj.name='TripoArch_Plant_'+name
        scene.collection.objects.link(obj)
        obj.hide_render=False
        obj.hide_set(False)
        bottom=min(vertex.co.z for vertex in obj.data.vertices)
        native_height=max(vertex.co.z for vertex in obj.data.vertices)-bottom
        factor=height/native_height
        obj.scale=(factor,factor,factor)
        obj.rotation_mode='XYZ'
        obj.rotation_euler=(0,0,math.radians(yaw))
        obj.location=(point.x,point.y,point.z-bottom*factor-.10)
        for slot in obj.material_slots:
            slot.link='OBJECT'
            slot.material=fern_mat if key=='Fern' else palm_mat
        obj['native_plant_source']=key
        obj['supported_on_rock']=True
        plants.append(obj)
        supports.append({'name':obj.name,'nativeSource':key,'base':list(point),'supportNormal':list(normal),'height':height})
    for params in [
        ('LeftHighPalm','PalmBent',-59,-7,23,16),
        ('LeftShoulderPalm','Palm',-77,-4,20,-24),
        ('RightShoulderPalm','PalmBent',67,2,18,-42)
    ]:
        place(*params)
    for index,params in enumerate([
        (-62,-9,2.6,12),(-57,-7,2.9,-28),(-60,-4,2.3,73),
        (-80,-5,2.8,-18),(-75,-6,2.5,38),(-78,-1,2.3,87),
        (64,0,2.7,31),(69,0,2.5,-33),(67,5,2.2,77),
        (-43,-12,2.4,52),(41,-10,2.2,-21)
    ]):
        place('LedgeFern'+str(index),'Fern',*params)
    bpy.context.view_layer.update()
    stats={'scene':scene.name,'stage':'NativeCC0PlantGroups','rockTriangles':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in rocks),'plantTriangles':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in plants),'rockInstances':len(rocks),'plantInstances':len(plants),'preparedFernTriangles':sum(len(p.vertices)-2 for p in fern_mesh.polygons),'supports':supports,'originalRockMeshesUnchanged':True}
    stats['totalTriangles']=stats['rockTriangles']+stats['plantTriangles']
    (OUTPUT/'tripo-arch-plant-stats.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding='utf-8')
    scene['stage']='NativeCC0PlantGroups'
    scene.camera=bpy.data.objects['TripoArch_CameraOverall']
    for obj in rocks+plants:
        for slot in obj.material_slots:
            if slot.material and slot.material.use_nodes:
                for node in slot.material.node_tree.nodes:
                    if node.type=='TEX_IMAGE' and node.image and not node.image.packed_file:
                        node.image.pack()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'SeaArch_Huge_A_Tripo.blend'),copy=True,compress=True)
    result=stats
finally:
    bpy.context.window.scene=previous
