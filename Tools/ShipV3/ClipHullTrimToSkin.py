import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
base=Path('D:/projects/Pirate_BR/PirateGame')
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text(encoding='utf-8'))
document=json.loads((folder.parent/'ShipV3.json').read_text(encoding='utf-8'))
vertices=[];triangles=[]
for piece in document['pieces']:
    if piece['family']!='Hull':continue
    obj=bpy.data.objects[piece['intact']]
    obj.data.calc_loop_triangles()
    matrix=obj.matrix_world.to_3x3().inverted().transposed()
    candidates=[]
    for face in obj.data.polygons:
        center=obj.matrix_world@face.center
        inward=Vector((-center.x,-center.y,0)).normalized()
        normal=(matrix@face.normal).normalized()
        if normal.dot(inward)>.16:candidates.append(face)
    if not candidates:continue
    basis=(matrix@max(candidates,key=lambda f:f.area).normal).normalized()
    offset=len(vertices)
    vertices.extend(obj.matrix_world@v.co for v in obj.data.vertices)
    triangles.extend(tuple(offset+i for i in tri.vertices) for tri in obj.data.loop_triangles if (matrix@obj.data.polygons[tri.polygon_index].normal).normalized().dot(-basis)>.8)
surface=BVHTree.FromPolygons(vertices,triangles,all_triangles=True)
def field(point):
    hit=surface.find_nearest(point)
    return (point-hit[0]).dot(hit[1])-.008 if hit[0] is not None else 1
modified=set(manifest['modified_objects']);empty=set(manifest['empty_objects']);changed=0
for piece in document['pieces']:
    if not piece['intact'].startswith('V3_Hull_Wale_'):continue
    if piece['bounds_min'][2]>8.85 or piece['bounds_max'][2]<manifest['floor_z']-.15:continue
    for name in [piece['intact']]+piece['fragment_names']:
        obj=bpy.data.objects[name];mesh=obj.data
        if not len(mesh.polygons):continue
        mesh.calc_loop_triangles();inverse=obj.matrix_world.inverted()
        uv_names=[layer.name for layer in mesh.uv_layers]
        positions=[obj.matrix_world@v.co for v in mesh.vertices]
        values=[field(p) for p in positions]
        vs=[];fs=[];mats=[];smooth=[];uvs=[[] for n in uv_names]
        for triangle in mesh.loop_triangles:
            polygon=[(positions[index],[mesh.uv_layers[n].data[loop].uv.copy() for n in uv_names],values[index]) for index,loop in zip(triangle.vertices,triangle.loops)]
            clipped=[];previous=polygon[-1]
            for current in polygon:
                if (current[2]>=0)!=(previous[2]>=0):
                    lo=0.;hi=1.
                    for iteration in range(12):
                        middle=(lo+hi)*.5
                        if (field(previous[0].lerp(current[0],middle))>=0)==(previous[2]>=0):lo=middle
                        else:hi=middle
                    t=(lo+hi)*.5
                    clipped.append((previous[0].lerp(current[0],t),[a.lerp(b,t) for a,b in zip(previous[1],current[1])],0))
                if current[2]>=0:clipped.append(current)
                previous=current
            for index in range(2,len(clipped)):
                tri=[clipped[0],clipped[index-1],clipped[index]];offset=len(vs)
                vs.extend(inverse@v[0] for v in tri);fs.append((offset,offset+1,offset+2))
                mats.append(triangle.material_index);smooth.append(mesh.polygons[triangle.polygon_index].use_smooth)
                for layer in range(len(uv_names)):uvs[layer].extend(v[1][layer] for v in tri)
        if len(fs)==len(mesh.loop_triangles) and all(v>=0 for v in values):continue
        output=bpy.data.meshes.new(name+'_OuterTrim')
        output.from_pydata(vs,[],fs)
        for material in mesh.materials:output.materials.append(material)
        for polygon,mat,sm in zip(output.polygons,mats,smooth):polygon.material_index=mat;polygon.use_smooth=sm
        for n,coords in zip(uv_names,uvs):
            layer=output.uv_layers.new(name=n)
            for loop,uv in zip(layer.data,coords):loop.uv=uv
        output.update();obj.data=output
        modified.add(name);changed+=1
        if not fs:empty.add(name)
manifest['modified_objects']=sorted(modified);manifest['empty_objects']=sorted(empty)
manifest['hull_trim_inside_clip_version']=2
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
result={'trim_meshes':changed,'clip':'true exterior skin','empty':len(empty)}
