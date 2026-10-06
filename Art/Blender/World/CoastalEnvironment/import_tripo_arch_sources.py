import bpy
import bmesh
import json
from mathutils import Vector
previous=bpy.context.scene
before_state={'file':bpy.data.filepath,'dirty':bpy.data.is_dirty,'activeScene':previous.name,'objects':len(previous.objects)}
scene=bpy.data.scenes.new('TripoRockSourceQC')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
inventory=json.load(open(r'D:\projects\Pirate_BR\output\CoastalEnvironment\tripo-rock-source-inventory.json',encoding='utf-8'))
report=[]
for item in inventory:
    before=set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=item['path'])
    objects=[obj for obj in bpy.data.objects if obj not in before]
    meshes=[obj for obj in objects if obj.type=='MESH']
    if len(meshes)!=1:raise RuntimeError('Expected one whole module: '+item['id']+' meshes='+str(len(meshes)))
    obj=meshes[0]
    matrix=obj.matrix_world.copy()
    obj.data=obj.data.copy()
    for vertex in obj.data.vertices:vertex.co=matrix @ vertex.co
    obj.parent=None
    obj.matrix_world.identity()
    points=[vertex.co for vertex in obj.data.vertices]
    low=Vector(tuple(min(point[i] for point in points) for i in range(3)))
    high=Vector(tuple(max(point[i] for point in points) for i in range(3)))
    center=(low+high)*.5
    for vertex in obj.data.vertices:vertex.co-=center
    obj.data.update()
    obj.name='TripoMaster_'+item['id']
    obj['SourceID']=item['id']
    obj['SourceFile']=item['path']
    obj.hide_render=True
    images=[]
    for material in obj.data.materials:
        if not material:continue
        material.name='TripoNative_'+item['id']
        for node in material.node_tree.nodes:
            if node.type=='TEX_IMAGE' and node.image:
                node.image.pack()
                images.append({'name':node.image.name,'packed':bool(node.image.packed_file),'size':list(node.image.size),'colourSpace':node.image.colorspace_settings.name})
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    boundary=sum(edge.is_boundary for edge in bm.edges)
    bm.free()
    report.append({'id':item['id'],'master':obj.name,'dimensions':list(high-low),'tris':sum(len(p.vertices)-2 for p in obj.data.polygons),'boundaryEdges':boundary,'UV':[uv.name for uv in obj.data.uv_layers],'images':images})
path=r'D:\projects\Pirate_BR\output\CoastalEnvironment\tripo-native-blender-stats.json'
json.dump(report,open(path,'w',encoding='utf-8'),indent=2)
bpy.context.window.scene=previous
result={'originalState':before_state,'nativeModules':report,'activeSceneRestored':previous.name}
