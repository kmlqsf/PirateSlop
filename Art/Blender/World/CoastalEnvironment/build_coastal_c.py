import bpy
import bmesh
import json
import math
import os
import random
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils import noise

project = r'D:\projects\Pirate_BR\PirateGame'
output = r'D:\projects\Pirate_BR\output\CoastalEnvironment'
directory = os.path.join(project, 'Art', 'Blender', 'World', 'CoastalEnvironment')
source = os.path.join(project, 'Art', 'Sources', 'CoastalEnvironment')
selection = globals().get('COASTAL_SELECTION', ['SeaArch_Huge_A'])
prototype = globals().get('COASTAL_PROTOTYPE', True)
write_exports = globals().get('COASTAL_EXPORT', False)
previous_scene = bpy.context.scene
scene_name = 'CoastalCPrototype' if prototype else 'CoastalEnvironment'
scene = bpy.data.scenes.get(scene_name) or bpy.data.scenes.new(scene_name)
if not globals().get('COASTAL_KEEP_SCENE',False):
    for obj in list(scene.objects):
        if prototype or any(obj.name.startswith(name + '_') for name in selection):
            bpy.data.objects.remove(obj, do_unlink=True)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
records = json.load(open(os.path.join(directory, 'physical-baseline.json'), encoding='utf-8'))['prefabs']

def stone_material():
    name = 'CoastalRockCPrototype' if prototype else 'CoastalRock'
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    shader = nodes.new('ShaderNodeBsdfPrincipled')
    shader.inputs['Roughness'].default_value = .84
    target = nodes.new('ShaderNodeOutputMaterial')
    links.new(shader.outputs[0], target.inputs['Surface'])
    diffuse = nodes.new('ShaderNodeTexImage')
    diffuse.image = bpy.data.images.load(os.path.join(source, 'rock_wall_02', 'rock_wall_02_diff_2k.png'), check_existing=True)
    tint = nodes.new('ShaderNodeMixRGB')
    tint.blend_type = 'MIX'
    tint.inputs[0].default_value = .14
    tint.inputs[2].default_value = (.61, .56, .46, 1)
    links.new(diffuse.outputs['Color'], tint.inputs[1])
    normal_image = nodes.new('ShaderNodeTexImage')
    normal_image.image = bpy.data.images.load(os.path.join(source, 'rock_wall_02', 'rock_wall_02_nor_gl_2k.png'), check_existing=True)
    normal_image.image.colorspace_settings.name = 'Non-Color'
    normal = nodes.new('ShaderNodeNormalMap')
    normal.inputs['Strength'].default_value = 1.15
    links.new(normal_image.outputs['Color'], normal.inputs['Color'])
    links.new(normal.outputs[0], shader.inputs['Normal'])
    coordinate = nodes.new('ShaderNodeTexCoord')
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = .085
    noise.inputs['Detail'].default_value = 2
    links.new(coordinate.outputs['Object'], noise.inputs['Vector'])
    variation = nodes.new('ShaderNodeMapRange')
    variation.inputs['From Min'].default_value = .15
    variation.inputs['From Max'].default_value = .85
    variation.inputs['To Min'].default_value = .66
    variation.inputs['To Max'].default_value = 1.13
    links.new(noise.outputs['Fac'], variation.inputs[0])
    color = nodes.new('ShaderNodeMixRGB')
    color.blend_type = 'MULTIPLY'
    color.inputs[0].default_value = 1
    links.new(tint.outputs[0], color.inputs[1])
    links.new(variation.outputs[0], color.inputs[2])
    separate = nodes.new('ShaderNodeSeparateXYZ')
    links.new(coordinate.outputs['Object'], separate.inputs[0])
    wet_noise = nodes.new('ShaderNodeMath')
    wet_noise.operation = 'MULTIPLY'
    wet_noise.inputs[1].default_value = 5
    links.new(noise.outputs['Fac'], wet_noise.inputs[0])
    wet_height = nodes.new('ShaderNodeMath')
    wet_height.operation = 'SUBTRACT'
    links.new(separate.outputs['Z'], wet_height.inputs[0])
    links.new(wet_noise.outputs[0], wet_height.inputs[1])
    wet = nodes.new('ShaderNodeMapRange')
    wet.clamp = True
    wet.inputs['From Min'].default_value = -.2
    wet.inputs['From Max'].default_value = 8
    wet.inputs['To Min'].default_value = .33
    wet.inputs['To Max'].default_value = 1
    links.new(wet_height.outputs[0], wet.inputs[0])
    final = nodes.new('ShaderNodeMixRGB')
    final.blend_type = 'MULTIPLY'
    final.inputs[0].default_value = 1
    links.new(color.outputs[0], final.inputs[1])
    links.new(wet.outputs[0], final.inputs[2])
    links.new(final.outputs[0], shader.inputs['Base Color'])
    moss_noise=nodes.new('ShaderNodeTexNoise')
    moss_noise.inputs['Scale'].default_value=.37
    moss_noise.inputs['Detail'].default_value=2
    links.new(coordinate.outputs['Object'],moss_noise.inputs['Vector'])
    moss_patch=nodes.new('ShaderNodeMapRange')
    moss_patch.clamp=True
    moss_patch.inputs['From Min'].default_value=.64
    moss_patch.inputs['From Max'].default_value=.79
    moss_patch.inputs['To Min'].default_value=0
    moss_patch.inputs['To Max'].default_value=.65
    links.new(moss_noise.outputs['Fac'],moss_patch.inputs[0])
    crevices=nodes.new('ShaderNodeNewGeometry')
    cavity=nodes.new('ShaderNodeMapRange')
    cavity.clamp=True
    cavity.inputs['From Min'].default_value=.48
    cavity.inputs['From Max'].default_value=.39
    cavity.inputs['To Min'].default_value=0
    cavity.inputs['To Max'].default_value=1
    links.new(crevices.outputs['Pointiness'],cavity.inputs[0])
    moss_mask=nodes.new('ShaderNodeMath')
    moss_mask.operation='MULTIPLY'
    links.new(moss_patch.outputs[0],moss_mask.inputs[0])
    links.new(cavity.outputs[0],moss_mask.inputs[1])
    moss=nodes.new('ShaderNodeMixRGB')
    links.new(moss_mask.outputs[0],moss.inputs[0])
    links.new(final.outputs[0],moss.inputs[1])
    moss.inputs[2].default_value=(.12,.18,.045,1)
    links.new(moss.outputs[0],shader.inputs['Base Color'])
    return material

stone = stone_material()

def source_volume(record):
    vertices, faces = [], []
    for piece in record['renders']:
        offset = len(vertices)
        vertices.extend((v[0], -v[2], v[1]) for v in piece['vertices'])
        indices = piece['triangles']
        faces.extend(tuple(offset + indices[i+j] for j in range(3)) for i in range(0, len(indices), 3))
    bm = bmesh.new()
    mapping = [bm.verts.new(v) for v in vertices]
    for indices in faces:
        try:
            bm.faces.new([mapping[i] for i in indices])
        except ValueError:
            pass
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0002)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    seen = set()
    solids = []
    for face in bm.faces:
        if face in seen:
            continue
        pending, component = [face], []
        seen.add(face)
        while pending:
            current = pending.pop()
            component.append(current)
            for edge in current.edges:
                for neighbor in edge.link_faces:
                    if neighbor not in seen:
                        pending.append(neighbor)
                        seen.add(neighbor)
        volume = sum(f.verts[0].co.dot(f.verts[1].co.cross(f.verts[2].co)) for f in component)
        if volume < 0:
            for current in component:
                current.normal_flip()
        points = {vertex for current in component for vertex in current.verts}
        center = sum((vertex.co for vertex in points), Vector())/len(points)
        planes = []
        for current in component:
            normal = current.normal.copy()
            if normal.dot(current.calc_center_median()-center) < 0:
                normal = -normal
            planes.append((normal.x,normal.y,normal.z,normal.dot(current.verts[0].co)))
        solids.append((Vector(tuple(min(v.co[a] for v in points) for a in range(3))),Vector(tuple(max(v.co[a] for v in points) for a in range(3))),planes))
    bm.verts.ensure_lookup_table()
    bm.verts.index_update()
    tree = BVHTree.FromBMesh(bm)
    low = Vector(tuple(min(v.co[a] for v in bm.verts) for a in range(3)))
    high = Vector(tuple(max(v.co[a] for v in bm.verts) for a in range(3)))
    bin_size=max(high-low)*.06
    buckets={}
    for solid in solids:
        minimum,maximum,planes=solid
        for i in range(math.floor(minimum.x/bin_size),math.floor(maximum.x/bin_size)+1):
            for j in range(math.floor(minimum.y/bin_size),math.floor(maximum.y/bin_size)+1):
                buckets.setdefault((i,j),[]).append(solid)
    cache = {}
    def intervals(x, y):
        key = (round(x, 5), round(y, 5))
        if key in cache:
            return cache[key]
        spans = []
        for minimum, maximum, planes in buckets.get((math.floor(x/bin_size),math.floor(y/bin_size)),[]):
            if x < minimum.x or x > maximum.x or y < minimum.y or y > maximum.y:
                continue
            lower, upper = minimum.z, maximum.z
            for normal_x,normal_y,normal_z,distance in planes:
                remaining = distance-normal_x*x-normal_y*y
                if abs(normal_z) < .00001:
                    if remaining < -.001:
                        lower, upper = 1,0
                        break
                elif normal_z > 0:
                    upper = min(upper,remaining/normal_z)
                else:
                    lower = max(lower,remaining/normal_z)
                if lower >= upper:
                    break
            if upper-lower > .03:
                spans.append((lower+.012,upper-.012))
        values = []
        for lower,upper in sorted(spans):
            if values and lower <= values[-1][1]+.025:
                values[-1] = (values[-1][0],max(values[-1][1],upper))
            else:
                values.append((lower,upper))
        cache[key] = values
        return values
    def inside(point, tolerance=.03):
        return any(a-tolerance <= point.z <= b+tolerance for a,b in intervals(point.x, point.y))
    return bm, tree, low, high, intervals, inside

def clip_polygon(polygon, normal, offset):
    result = []
    for index, current in enumerate(polygon):
        previous = polygon[index-1]
        a, b = previous.dot(normal)-offset, current.dot(normal)-offset
        if (a <= 0) != (b <= 0):
            result.append(previous+(current-previous)*(a/(a-b)))
        if b <= 0:
            result.append(current)
    return result

def slab_shell(record):
    bm, tree, low, high, intervals, inside = source_volume(record)
    extent = high-low
    maximum = max(extent)
    large = record['name'].startswith(('Sea_', 'SeaArch_', 'Reef_'))
    period = max(.75, maximum*(.042 if large else .16))
    physical_bm = None
    if record['name'] == 'SeaArch_Huge_A':
        physical = dict(record)
        physical['renders'] = record['colliders']
        physical_bm, physical_tree, physical_low, physical_high, physical_intervals, physical_inside = source_volume(physical)
        profile = [(-49,-7),(-44,14),(-38,41),(-30,60),(-20,70),(-10,77),(0,78),(10,77),(20,70),(30,62),(38,42),(44,14),(49,-7)]
        def fallback_roof(x):
            for index in range(len(profile)-1):
                a,b = profile[index],profile[index+1]
                if a[0] <= x <= b[0]:
                    t=(x-a[0])/(b[0]-a[0])
                    return a[1]+(b[1]-a[1])*t
            return -7
        def arch_intervals(x,y):
            width = 88+2.5*math.sin(y*.16)
            front = -26-4*math.sin(x*.085+.5)
            back = 31+3*math.sin(x*.11)
            if abs(x)>width or y<front or y>back:
                over=max(abs(x)-width,front-y,y-back,0)
                if abs(x)>high.x-1 or x<low.x+1 or y<low.y+1 or y>high.y-1 or over>9 or abs(x)<43:
                    return []
                return [(-7,13-over*.95+1.7*math.sin(x*.53+y*.34))]
            roof = fallback_roof(x)
            if abs(x)<49:
                obstacles = [(a,b) for a,b in physical_intervals(x,y) if b>25]
                if obstacles:
                    roof = min(a for a,b in obstacles)+.14
            roof = max(-7,roof)
            top = min(high.z-.8,97-.12*(x+70)+1.2*math.sin(x*.21))
            top = math.floor(top/2.3)*2.3
            if top-roof < 1:
                return []
            return [(roof,top)]
        intervals = arch_intervals
        def inside(point,tolerance=.03):
            return any(a-tolerance<=point.z<=b+tolerance for a,b in intervals(point.x,point.y))
    rng = random.Random(record['name']+'_NaturalSlabsC')
    grid = {}
    nx, ny = math.ceil(extent.x/period)+2, math.ceil(extent.y/period)+2
    for i in range(nx):
        for j in range(ny):
            grid[i,j] = Vector((low.x+(i-.35+rng.uniform(-.31,.31))*period, low.y+(j-.35+rng.uniform(-.31,.31))*period))
    new_vertices, new_faces, slots = [], [], []
    rejected, columns = 0, 0
    for (i,j), center in grid.items():
        vertical = intervals(center.x, center.y)
        if not vertical:
            continue
        polygon = [center+Vector(v)*period for v in ((-2,-2),(2,-2),(2,2),(-2,2))]
        for ni in range(max(0,i-2), min(nx,i+3)):
            for nj in range(max(0,j-2), min(ny,j+3)):
                if (ni,nj) == (i,j):
                    continue
                neighbor = grid[ni,nj]
                normal = neighbor-center
                polygon = clip_polygon(polygon, normal, (neighbor.length_squared-center.length_squared)*.5)
                if len(polygon) < 3:
                    break
        if len(polygon) < 3:
            continue
        for bottom, top in vertical:
            if top < -.5 or top-bottom < period*.12:
                continue
            if record['name']=='Reef_Spires_B' and bottom > 0 and top-bottom < extent.z*.16:
                continue
            original = polygon
            shape = []
            for k, point in enumerate(original):
                shape.append(point)
                other = original[(k+1)%len(original)]
                middle = point.lerp(other, rng.uniform(.35,.63))
                middle = middle.lerp(center, rng.uniform(.07,.18))
                shape.append(middle)
            for attempt in range(5):
                factor = .985*(.82**attempt)
                corners = [center+(point-center)*factor for point in shape]
                spans = []
                for corner_index, point in enumerate(corners):
                    choices = []
                    for corner_shrink in (1,.84,.66,.5,.32,.15):
                        test = center+(point-center)*corner_shrink
                        choices = [(a,b) for a,b in intervals(test.x, test.y) if min(b,top)-max(a,bottom) > (top-bottom)*.35]
                        if choices:
                            corners[corner_index] = test
                            break
                    if not choices:
                        spans = []
                        break
                    spans.append(max(choices, key=lambda ab:min(ab[1],top)-max(ab[0],bottom)))
                if not spans:
                    continue
                lower = max(a for a,b in spans)
                upper = min(b for a,b in spans)
                if upper-lower < period*.1:
                    continue
                ring_vertices = []
                levels = (0, .16, .36, .58, .79, 1)
                drift = Vector((rng.uniform(-.17,.17),rng.uniform(-.17,.17)))*period
                phases = [rng.uniform(-1,1) for point in corners]
                for layer, t in enumerate(levels):
                    for k, point in enumerate(corners):
                        scale = (.99, 1, .98, .995, .96, .97)[layer]
                        ridge = .027*math.sin(t*math.pi*1.8+phases[k]*2.5)
                        xy = center+(point-center)*(scale+ridge)+drift*math.sin(t*math.pi)
                        available = [(a,b) for a,b in intervals(xy.x,xy.y) if min(b,top)-max(a,bottom) > (top-bottom)*.15]
                        if not available:
                            for shrink in (.9,.8,.65,.4,.2):
                                xy = center+(xy-center)*shrink
                                available = [(a,b) for a,b in intervals(xy.x,xy.y) if min(b,top)-max(a,bottom) > (top-bottom)*.15]
                                if available:
                                    break
                        if not available:
                            ring_vertices = []
                            break
                        a,b = max(available,key=lambda ab:min(ab[1],top)-max(ab[0],bottom))
                        local_bottom = max(bottom,a)
                        local_top = min(top,b)
                        z = local_bottom+(local_top-local_bottom)*t
                        if layer == len(levels)-1:
                            z -= min(period*.16,(local_top-local_bottom)*.10)*(phases[k]+1)*.5
                        z = min(b-.015,max(a+.015,z))
                        ring_vertices.append(Vector((xy.x,xy.y,z)))
                    if not ring_vertices:
                        break
                if not ring_vertices:
                    continue
                n = len(corners)
                faces = []
                for layer in range(len(levels)-1):
                    for k in range(n):
                        nxt = (k+1)%n
                        faces.append((layer*n+k,layer*n+nxt,(layer+1)*n+nxt,(layer+1)*n+k))
                bottom_center = sum((ring_vertices[k] for k in range(n)),Vector())/n
                top_center = sum((ring_vertices[(len(levels)-1)*n+k] for k in range(n)),Vector())/n
                center_spans = intervals(bottom_center.x,bottom_center.y)
                if center_spans:
                    bottom_center.z = max(bottom_center.z,center_spans[-1][0]+.04)
                    top_center.z = min(center_spans[-1][1]-.04,top_center.z+period*.065)
                bottom_index,top_index=len(ring_vertices),len(ring_vertices)+1
                ring_vertices.extend((bottom_center,top_center))
                for k in range(n):
                    nxt=(k+1)%n
                    faces.append((nxt,k,bottom_index))
                    faces.append(((len(levels)-1)*n+k,(len(levels)-1)*n+nxt,top_index))
                valid = True
                for face in faces:
                    if not inside(sum((ring_vertices[k] for k in face),Vector())/len(face),period*.09):
                        valid = False
                        break
                    for k in range(len(face)):
                        if not inside(ring_vertices[face[k]].lerp(ring_vertices[face[k-1]],.5),period*.09):
                            valid = False
                            break
                    if not valid:
                        break
                if not valid:
                    continue
                offset = len(new_vertices)
                new_vertices.extend(tuple(v) for v in ring_vertices)
                new_faces.extend(tuple(offset+k for k in f) for f in faces)
                columns += 1
                break
            else:
                rejected += 1
    mesh = bpy.data.meshes.new(record['name']+'_NaturalSlabShell')
    mesh.from_pydata(new_vertices, [], new_faces)
    mesh.update()
    obj = bpy.data.objects.new(record['name']+'_FracturedStone', mesh)
    scene.collection.objects.link(obj)
    mesh.materials.append(stone)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    obj.data.remesh_voxel_size = period*.13
    bpy.ops.object.voxel_remesh()
    mesh = obj.data
    mesh.update()
    sites = list(grid.values())
    breaks = {}
    for index,site in enumerate(sites):
        local = intervals(site.x,site.y)
        if local:
            a,b = local[-1]
            fractures=[]
            z=a+rng.uniform(3,8)
            while z<b-2:
                fractures.append(z)
                z+=rng.uniform(3,8)
            breaks[index] = (a+(b-a)*rng.uniform(.28,.41),a+(b-a)*rng.uniform(.63,.79),rng.uniform(-.13,.13),rng.uniform(-.13,.13),fractures)
    for vertex in mesh.vertices:
        p = vertex.co.copy()
        n = vertex.normal.copy()
        cell_x=round((p.x-low.x)/period+.35)
        cell_y=round((p.y-low.y)/period+.35)
        nearby=[]
        for ni in range(max(0,cell_x-2),min(nx,cell_x+3)):
            for nj in range(max(0,cell_y-2),min(ny,cell_y+3)):
                site=grid[ni,nj]
                nearby.append(((p.x-site.x)**2+(p.y-site.y)**2,ni*ny+nj))
        nearest=sorted(nearby)[:2]
        index = nearest[0][1]
        seam = (math.sqrt(nearest[1][0])-math.sqrt(nearest[0][0]))*.5
        weathering = noise.noise(Vector((p.x/period*2.7,p.y/period*2.7,p.z/period*4.2)),noise_basis='PERLIN_ORIGINAL')
        depth = period*(.025+.018*weathering)
        if abs(n.z)<.82 and index in breaks:
            first,second,tilt_x,tilt_y,fractures = breaks[index]
            site = sites[index]
            shift=(p.x-site.x)*tilt_x+(p.y-site.y)*tilt_y
            distance=min(abs(p.z-first-shift),abs(p.z-second-shift))
            fracture=max(0,1-distance/(period*.15))
            interrupted=.55+.45*noise.noise(Vector((p.x/period*1.8,p.y/period*1.8,p.z/period*.4)),noise_basis='PERLIN_ORIGINAL')
            depth += period*.18*fracture*interrupted
            if fractures:
                minor=min(abs(p.z-z-shift) for z in fractures)
                depth += min(.48,period*.05)*max(0,1-minor/.7)*interrupted
            depth += period*.105*max(0,1-seam/(period*.12))*(.55+.45*weathering)
        vertex.co -= n*depth
        available = intervals(vertex.co.x,vertex.co.y)
        if available:
            a,b=min(available,key=lambda ab:max(ab[0]-vertex.co.z,vertex.co.z-ab[1],0))
            vertex.co.z=max(a+.03,min(b-.03,vertex.co.z))
    mesh.update()
    budget = 22000 if large else 1600
    mesh.calc_loop_triangles()
    if len(mesh.loop_triangles)>budget:
        reduction=obj.modifiers.new('CoastalGeometryBudget','DECIMATE')
        reduction.ratio=budget/len(mesh.loop_triangles)
        bpy.ops.object.modifier_apply(modifier=reduction.name)
        mesh=obj.data
    uv = mesh.uv_layers.new(name='UVMap')
    tile = .18 if large else .7
    for polygon in mesh.polygons:
        normal = polygon.normal
        axis = max(range(3),key=lambda a:abs(normal[a]))
        for index in polygon.loop_indices:
            point = mesh.vertices[mesh.loops[index].vertex_index].co
            coordinate = (point.y,point.z) if axis==0 else (point.x,point.z) if axis==1 else (point.x,point.y)
            uv.data[index].uv = (coordinate[0]*tile,coordinate[1]*tile)
        polygon.use_smooth = True
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    split = obj.modifiers.new('NaturalCreases','EDGE_SPLIT')
    split.split_angle = .95
    split.use_edge_angle = True
    bpy.ops.object.modifier_apply(modifier=split.name)
    triangulation = obj.modifiers.new('CoastalTriangles','TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=triangulation.name)
    invalid_vertices = sum(not inside(v.co,.3) for v in obj.data.vertices)
    obj['EnvelopeValidation'] = invalid_vertices
    obj['NaturalSlabCount'] = columns
    obj['Physics'] = 'Visual only; every source vertex and sampled face stays inside the original union.'
    bm.free()
    if physical_bm:
        physical_bm.free()
    return obj, {'name':record['name'],'stoneTris':len(obj.data.polygons),'slabs':columns,'rejectedCells':rejected,'outsideVertices':invalid_vertices,'bounds':{'min':list(low),'max':list(high)}}

def feather_palm(name,height=20,bend=.065,level=0):
    template=bpy.data.objects.get('Template_Palm')
    material=bpy.data.materials.get('CoastalPalm')
    leaf_uv=Vector((.5,.5))
    stem_uv=Vector((.5,.5))
    texture=next((n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and 'diffuse' in n.image.name.lower()),None) if material else None
    pixels=texture.pixels[:] if texture else None
    def pixel(uv):
        if not pixels:return (1,1,1,1)
        x=min(texture.size[0]-1,max(0,int(uv.x*texture.size[0])))
        y=min(texture.size[1]-1,max(0,int(uv.y*texture.size[1])))
        start=4*(x+y*texture.size[0])
        return pixels[start:start+4]
    if template and template.data.uv_layers:
        layer=template.data.uv_layers.active
        leaves,stems=[],[]
        for polygon in template.data.polygons:
            center=polygon.center
            uv_center=sum((layer.data[i].uv for i in polygon.loop_indices),Vector((0,0)))/len(polygon.loop_indices)
            rgba=pixel(uv_center)
            if rgba[3]<.98:continue
            if center.z>.72 and Vector((center.x,center.y)).length>.08:
                corners=[pixel(uv_center+Vector((dx,dy))) for dx,dy in [(-.003,-.009),(-.003,.009),(.003,-.009),(.003,.009)]]
                if min(c[3] for c in corners)>.98:
                    leaves.append((rgba[1]-rgba[0]*.85,uv_center.copy()))
            if .2<center.z<.45 and Vector((center.x,center.y)).length<.06:
                stems.append((rgba[0]-rgba[1],uv_center.copy()))
        if leaves:leaf_uv=max(leaves,key=lambda item:item[0])[1]
        if stems:stem_uv=max(stems,key=lambda item:item[0])[1]
    if texture and pixel(leaf_uv)[3]<.98:
        best=None
        step=max(1,texture.size[0]//128)
        for y in range(step,texture.size[1]-step,step):
            for x in range(step,texture.size[0]-step,step):
                uv_point=Vector(((x+.5)/texture.size[0],(y+.5)/texture.size[1]))
                rgba=pixel(uv_point)
                if rgba[3]<.99 or rgba[1]<rgba[0]*.84 or rgba[1]<.12:
                    continue
                corners=[pixel(uv_point+Vector((dx,dy))) for dx,dy in [(-.003,-.009),(-.003,.009),(.003,-.009),(.003,.009)]]
                if min(c[3] for c in corners)<.99:
                    continue
                score=rgba[1]-rgba[0]*.75-rgba[2]*.12
                if best is None or score>best[0]:best=(score,uv_point)
        if best:leaf_uv=best[1]
    if pixel(leaf_uv)[3]<.98 or pixel(stem_uv)[3]<.98:
        raise RuntimeError('Palm atlas requires opaque leaf and stem patches.')
    if material:
        nodes=material.node_tree.nodes
        links=material.node_tree.links
        tint=next((n for n in nodes if n.type=='MIX_RGB'),None)
        diffuse=next((n for n in nodes if n.type=='TEX_IMAGE' and n.image==texture),None)
        if tint and diffuse and not nodes.get('LeafHueMask'):
            separate=nodes.new('ShaderNodeSeparateColor')
            links.new(diffuse.outputs['Color'],separate.inputs['Color'])
            red=nodes.new('ShaderNodeMath')
            red.operation='MULTIPLY'
            red.inputs[1].default_value=.85
            links.new(separate.outputs['Red'],red.inputs[0])
            green=nodes.new('ShaderNodeMath')
            green.operation='SUBTRACT'
            links.new(separate.outputs['Green'],green.inputs[0])
            links.new(red.outputs[0],green.inputs[1])
            mask=nodes.new('ShaderNodeMapRange')
            mask.name='LeafHueMask'
            mask.inputs['From Min'].default_value=.015
            mask.inputs['From Max'].default_value=.12
            mask.inputs['To Min'].default_value=0
            mask.inputs['To Max'].default_value=.28
            mask.clamp=True
            links.new(green.outputs[0],mask.inputs[0])
            links.new(mask.outputs[0],tint.inputs[0])
    rng=random.Random(name+'_FeatherPalm')
    vertices,faces,patches=[],[],[]
    sides=7 if level==0 else 5
    rings=11 if level==0 else 7
    for ring in range(rings):
        t=ring/(rings-1)
        center=Vector((height*bend*t*t,height*.018*math.sin(t*math.pi),height*.88*t))
        radius=height*(.009*(1-t)+.005*t)
        for side in range(sides):
            angle=math.tau*side/sides
            vertices.append(tuple(center+Vector((math.cos(angle)*radius,math.sin(angle)*radius,0))))
    for ring in range(rings-1):
        for side in range(sides):
            nxt=(side+1)%sides
            faces.append((ring*sides+side,ring*sides+nxt,(ring+1)*sides+nxt,(ring+1)*sides+side))
            patches.append(stem_uv)
    faces.append(tuple(reversed(range(sides))))
    patches.append(stem_uv)
    root=Vector((height*bend,0,height*.88))
    branches=8 if level==0 else 6 if level==1 else 5
    for branch in range(branches+3):
        young=branch>=branches
        angle=math.tau*(branch%branches)/branches+rng.uniform(-.12,.12)+(1.0 if young else 0)
        direction=Vector((math.cos(angle),math.sin(angle),0))
        side=Vector((-direction.y,direction.x,0))
        length=height*(rng.uniform(.23,.28) if young else rng.uniform(.40,.48))
        origin=root+direction*height*.018+Vector((0,0,height*.015*(branch%3)))
        def centerline(t):
            z=height*(.20*t-.10*t*t) if young else height*(.10*math.sin(t*math.pi)-.18*t*t)
            return origin+direction*length*t+Vector((0,0,z))
        segments=8 if level==0 else 5 if level==1 else 3
        for segment in range(segments):
            t0,t1=segment/segments,(segment+1)/segments
            a,b=centerline(t0),centerline(t1)
            width=height*((.026 if young else .048)*math.sin((t0+.12)*math.pi*.84)+.0025)
            index=len(vertices)
            vertices.extend((tuple(a-side*width),tuple(a+side*width),tuple(b+side*width*.8),tuple(b-side*width*.8)))
            faces.append((index,index+1,index+2,index+3))
            patches.append(leaf_uv)
        leaves=12 if level==0 else 7 if level==1 else 4
        for segment in range(1,leaves+1):
            t=segment/(leaves+1)
            point=centerline(t)
            leaflet=height*(.11 if not young else .07)*math.sin(t*math.pi)*rng.uniform(.8,1.15)
            for sign in (-1,1):
                axis=(side*sign-direction*.18).normalized()
                tip=point+axis*leaflet+direction*length*.035+Vector((0,0,-height*.012-leaflet*.09))
                middle=point.lerp(tip,.48)+Vector((0,0,height*.011))
                width=leaflet*.15
                index=len(vertices)
                vertices.extend((tuple(point),tuple(middle+direction*width),tuple(tip),tuple(middle-direction*width)))
                faces.append((index,index+1,index+2,index+3))
                patches.append(leaf_uv)
    mesh=bpy.data.meshes.new(name+'_PalmMesh')
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    uv=mesh.uv_layers.new(name='UVMap')
    for polygon,patch in zip(mesh.polygons,patches):
        polygon.use_smooth=True
        count=len(polygon.loop_indices)
        for order,index in enumerate(polygon.loop_indices):
            angle=math.tau*order/count
            uv.data[index].uv=patch+Vector((math.cos(angle)*.003,math.sin(angle)*.009))
    if material:
        mesh.materials.append(material)
    obj=bpy.data.objects.new(name+'_Palm',mesh)
    scene.collection.objects.link(obj)
    obj['PalmHeight']=height
    obj['PalmBend']=bend
    obj['FoliageClass']='Palm'
    obj['LeafUV']=list(leaf_uv)
    obj['StemUV']=list(stem_uv)
    obj['LeafRGBA']=list(pixel(leaf_uv))
    obj['StemRGBA']=list(pixel(stem_uv))
    return obj

def vegetation(name,obj):
    if not name.startswith(('Sea_', 'SeaArch_', 'Reef_')) or name=='Reef_ShallowField_A':
        return []
    tree=BVHTree.FromPolygons([v.co for v in obj.data.vertices],[tuple(p.vertices) for p in obj.data.polygons],all_triangles=True)
    minimum=Vector(tuple(min(v.co[a] for v in obj.data.vertices) for a in range(3)))
    maximum=Vector(tuple(max(v.co[a] for v in obj.data.vertices) for a in range(3)))
    size=maximum-minimum
    candidates=[]
    for polygon in obj.data.polygons:
        if polygon.normal.z>.86 and polygon.center.z>size.z*.45:
            candidates.append(polygon.center.copy())
    rng=random.Random(name+'_ProtectedVegetation')
    rng.shuffle(candidates)
    positions=[]
    for point in candidates:
        if all((point-other).length>max(size.x,size.y)*.22 for other in positions):
            positions.append(point)
        if len(positions)==3:
            break
    plants=[]
    fern_template=bpy.data.objects.get('Template_Fern')
    for index,point in enumerate(positions[:2]):
            height=min(22,max(10,size.z*.22))*(1-.20*index)
            plant=feather_palm(name+'_Ledge'+str(index),height,rng.uniform(-.065,.095))
            plant.location=point-Vector((0,0,.12))
            plant.rotation_euler.z=rng.uniform(0,math.tau)
            plant.hide_render=False
            plant.hide_set(False)
            plants.append(plant)
    if fern_template:
        for index,point in enumerate(positions):
            for offset in [Vector((-.9,.35,0)),Vector((1.1,-.4,0))]:
                target=point+offset
                hit,normal,face,distance=tree.ray_cast(Vector((target.x,target.y,maximum.z+2)),Vector((0,0,-1)),size.z+3)
                if hit is None or normal.z<.72:
                    continue
                plant=fern_template.copy()
                plant.data=fern_template.data.copy()
                plant.name=name+'_LedgeFern'+str(len(plants))
                plant.scale=(rng.uniform(2.7,4),)*3
                plant.location=hit-Vector((0,0,.05))
                plant.rotation_euler.z=rng.uniform(0,math.tau)
                plant.hide_render=False
                scene.collection.objects.link(plant)
                plant.hide_set(False)
                bpy.ops.object.select_all(action='DESELECT')
                plant.select_set(True)
                bpy.context.view_layer.objects.active=plant
                reduction=plant.modifiers.new('FoliageBudget','DECIMATE')
                reduction.ratio=.105
                bpy.ops.object.modifier_apply(modifier=reduction.name)
                plants.append(plant)
    return plants

def activate(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]

def export_lods(name,objects):
    rows=[]
    large=name.startswith(('Sea_', 'SeaArch_', 'Reef_'))
    stone_budgets=(25000,5000,1400) if large else (1800,440,120)
    for level in range(3):
        copies=[]
        for original in objects:
            if original.get('FoliageClass')=='Palm' and level:
                obj=feather_palm(original.name+'_LOD'+str(level),original['PalmHeight'],original['PalmBend'],level)
                obj.matrix_world=original.matrix_world.copy()
            else:
                obj=original.copy()
                obj.data=original.data.copy()
                scene.collection.objects.link(obj)
            obj.hide_render=False
            obj.hide_set(False)
            activate([obj])
            obj.data.calc_loop_triangles()
            count=len(obj.data.loop_triangles)
            if '_FracturedStone' in original.name:
                target=stone_budgets[level]
                obj.data.materials.clear()
                obj.data.materials.append(bpy.data.materials.get('CoastalRock') or stone)
            elif 'Fern' in original.name:
                target=count if level==0 else 75 if level==1 else 25
            else:
                target=count
            if count>target:
                reduction=obj.modifiers.new('CoastalLOD'+str(level),'DECIMATE')
                reduction.ratio=target/count
                bpy.ops.object.modifier_apply(modifier=reduction.name)
            copies.append(obj)
        activate(copies)
        bpy.ops.object.join()
        joined=bpy.context.object
        joined.name=name+'_LOD'+str(level)
        joined.data.calc_loop_triangles()
        tris=len(joined.data.loop_triangles)
        slots=[material.name if material else '' for material in joined.data.materials]
        path=os.path.join(project,'Assets','Models','World','CoastalEnvironment',name+'_LOD'+str(level)+'.fbx')
        bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
        rows.append({'level':level,'tris':tris,'renderers':1,'slots':slots,'path':path})
        data=joined.data
        bpy.data.objects.remove(joined,do_unlink=True)
        bpy.data.meshes.remove(data)
    return rows

reports = []
for record in records:
    if record['name'] not in selection or record['name'] in ('Palm','PalmBent','PalmYoung','Bush','Fern'):
        continue
    obj, report = slab_shell(record)
    plants=vegetation(record['name'],obj)
    report['plantTris']=sum(sum(len(p.vertices)-2 for p in plant.data.polygons) for plant in plants)
    if write_exports:
        report['lods']=export_lods(record['name'],[obj]+plants)
    reports.append(report)
scene['Concept'] = 'C: continuous irregular vertical limestone plates, no ico or bevelled cube source geometry.'
json.dump(reports,open(os.path.join(output,'prototype-c-manifest.json'),'w',encoding='utf-8'),indent=2)
if write_exports:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(directory,'CoastalEnvironment.blend'),copy=True)
bpy.context.window.scene = previous_scene
result = {'scene':scene.name,'models':reports,'exportsChanged':write_exports}
