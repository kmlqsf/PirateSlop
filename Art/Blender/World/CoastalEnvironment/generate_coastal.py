import bpy
import bmesh
import json
import math
import os
import random
from mathutils import Vector
from mathutils.bvhtree import BVHTree

project = r'D:\projects\Pirate_BR\PirateGame'
output = r'D:\projects\Pirate_BR\output\CoastalEnvironment'
source = os.path.join(project, 'Art', 'Sources', 'CoastalEnvironment')
export = os.path.join(project, 'Assets', 'Models', 'World', 'CoastalEnvironment')
os.makedirs(export, exist_ok=True)
baseline = json.load(open(os.path.join(project, 'Art', 'Blender', 'World', 'CoastalEnvironment', 'physical-baseline.json'), encoding='utf-8'))
selection = globals().get('COASTAL_SELECTION')
if selection:
    baseline['prefabs'] = [record for record in baseline['prefabs'] if record['name'] in selection]
previous_scene = bpy.context.scene
if previous_scene.name == 'CoastalEnvironment':
    previous_scene = bpy.data.scenes.get('Scene', previous_scene)
probe = bpy.data.objects.new('CoastalBridgeProbe', None)
previous_scene.collection.objects.link(probe)
probe.location = (1, 2, 3)
assert tuple(probe.location) == (1, 2, 3)
bpy.data.objects.remove(probe, do_unlink=True)
scene = bpy.data.scenes.get('CoastalEnvironment')
if scene is None:
    scene = bpy.data.scenes.new('CoastalEnvironment')
else:
    for obj in list(scene.objects):
        if not selection or obj.name.startswith('Template_') or any(obj.name.startswith(name+'_') for name in selection):
            bpy.data.objects.remove(obj, do_unlink=True)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
materials = {}

def material(name, color, texture=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.node_tree.nodes.clear()
    principled = mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    target = mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
    mat.node_tree.links.new(principled.outputs[0], target.inputs['Surface'])
    principled.inputs['Base Color'].default_value = (*color, 1)
    principled.inputs['Roughness'].default_value = .82
    if texture:
        tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = bpy.data.images.load(texture, check_existing=True)
        tex.projection = 'BOX'
        tex.projection_blend = .2
        coord = mat.node_tree.nodes.new('ShaderNodeTexCoord')
        mapping = mat.node_tree.nodes.new('ShaderNodeVectorMath')
        mapping.operation = 'SCALE'
        mapping.inputs[3].default_value = .12
        mat.node_tree.links.new(coord.outputs['Object'], mapping.inputs[0])
        mat.node_tree.links.new(mapping.outputs[0], tex.inputs['Vector'])
        tint = mat.node_tree.nodes.new('ShaderNodeMixRGB')
        tint.blend_type = 'MIX'
        tint.inputs[0].default_value = .55
        tint.inputs[2].default_value = (*color, 1)
        mat.node_tree.links.new(tex.outputs['Color'], tint.inputs[1])
        mat.node_tree.links.new(tint.outputs[0], principled.inputs['Base Color'])
    materials[name] = mat
    return mat

rock_texture = os.path.join(source, 'rock_face_02', 'textures', 'rock_face_02_diff_2k.png')
material('CoastalRock', (.56, .52, .43), rock_texture)
material('CoastalRockDark', (.29, .29, .24), rock_texture)
material('CoastalRockLight', (.63, .58, .48), rock_texture)
material('CoastalRockWarm', (.47, .43, .34), rock_texture)
material('CoastalMoss', (.27, .33, .12))
material('CoastalBark', (.31, .22, .13))
material('CoastalLeaf', (.24, .36, .10))
material('CoastalLeafLight', (.36, .46, .16))

def activate(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

def mesh_object(name, vertices, faces, mat):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.data.materials.append(materials[mat])
    return obj

def convert(v):
    return Vector((v[0], -v[2], v[1]))

def palm(name, height, bend=0):
    vertices, faces = [], []
    sides, levels = 10, 18
    for i in range(levels + 1):
        t = i / levels
        radius = height * (.032 - .015 * t) * (1 + .09 * math.sin(i * 2.7))
        for j in range(sides):
            a = j * math.tau / sides
            vertices.append((bend * t * t + radius * math.cos(a), radius * math.sin(a), height * t))
    for i in range(levels):
        for j in range(sides):
            a = i * sides + j
            b = i * sides + (j + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    trunk = mesh_object(name + '_Bark', vertices, faces, 'CoastalBark')
    leaves_v, leaves_f = [], []
    rng = random.Random(name)
    for leaf in range(11):
        a = leaf * math.tau / 11 + rng.uniform(-.13, .13)
        direction = Vector((math.cos(a), math.sin(a), 0))
        side = Vector((-math.sin(a), math.cos(a), 0))
        length = height * rng.uniform(.38, .49)
        origin = Vector((bend, 0, height))
        for segment in range(10):
            t0, t1 = segment / 10, (segment + 1) / 10
            p0 = origin + direction * length * t0 + Vector((0, 0, height * (.09 * math.sin(t0 * math.pi) - .14 * t0 * t0)))
            p1 = origin + direction * length * t1 + Vector((0, 0, height * (.09 * math.sin(t1 * math.pi) - .14 * t1 * t1)))
            width = length * .095 * math.sin((t0 + .05) * math.pi)
            index = len(leaves_v)
            leaves_v.extend((tuple(p0), tuple(p1), tuple(p0 + side * width - direction * length * .08 + Vector((0, 0, -.09 * height * t0))), tuple(p0 - side * width - direction * length * .08 + Vector((0, 0, -.09 * height * t0)))))
            leaves_f.extend(((index, index + 2, index + 1), (index, index + 1, index + 3)))
    crown = mesh_object(name + '_Leaf', leaves_v, leaves_f, 'CoastalLeaf')
    crown.data.materials.append(materials['CoastalLeafLight'])
    for face in crown.data.polygons:
        face.material_index = 1 if face.index % 7 == 0 else 0
    return [trunk, crown]

def fern(name, height=1.2):
    vertices, faces = [], []
    for branch in range(9):
        angle = branch * math.tau / 9
        direction = Vector((math.cos(angle), math.sin(angle), 0))
        side = Vector((-math.sin(angle), math.cos(angle), 0))
        for segment in range(1, 9):
            t = segment / 9
            center = direction * height * t * .7 + Vector((0, 0, height * math.sin(t * math.pi * .85)))
            width = height * .18 * math.sin(t * math.pi)
            for sign in (-1, 1):
                i = len(vertices)
                tip = center + side * sign * width + direction * height * .1
                vertices.extend((tuple(center), tuple(tip), tuple(center + direction * height * .10 + Vector((0, 0, .04)))))
                faces.append((i, i + 1, i + 2))
    return mesh_object(name + '_Leaf', vertices, faces, 'CoastalLeaf')

def foliage_material(name, diffuse, normal, alpha=None):
    mat = material(name, (.75,.83,.65))
    nodes = mat.node_tree.nodes
    principled = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    texture = nodes.new('ShaderNodeTexImage')
    texture.image = bpy.data.images.load(diffuse, check_existing=True)
    tint = nodes.new('ShaderNodeMixRGB')
    tint.inputs[0].default_value = .30
    tint.inputs[2].default_value = (.24,.40,.075,1)
    mat.node_tree.links.new(texture.outputs['Color'],tint.inputs[1])
    mat.node_tree.links.new(tint.outputs[0], principled.inputs['Base Color'])
    principled.inputs['Roughness'].default_value = .76
    if alpha:
        mask = nodes.new('ShaderNodeTexImage')
        mask.image = bpy.data.images.load(alpha, check_existing=True)
        mat.node_tree.links.new(mask.outputs['Color'], principled.inputs['Alpha'])
    else:
        mat.node_tree.links.new(texture.outputs['Alpha'], principled.inputs['Alpha'])
    texture_normal = nodes.new('ShaderNodeTexImage')
    texture_normal.image = bpy.data.images.load(normal, check_existing=True)
    texture_normal.image.colorspace_settings.name = 'Non-Color'
    bump = nodes.new('ShaderNodeNormalMap')
    bump.inputs['Strength'].default_value = .25
    mat.node_tree.links.new(texture_normal.outputs['Color'], bump.inputs['Color'])
    mat.node_tree.links.new(bump.outputs[0], principled.inputs['Normal'])
    return mat

foliage_material('CoastalPalm', os.path.join(source,'yughues_palms','diffuse.tga'),os.path.join(source,'yughues_palms','normal.tga'))
foliage_material('CoastalFern', os.path.join(source,'fern_02','textures','fern_02_diff_2k.png'),os.path.join(source,'fern_02','textures','fern_02_nor_gl_2k.png'),os.path.join(source,'fern_02','textures','fern_02_alpha_2k.png'))
templates = {}
for key, path in [('Palm',os.path.join(source,'yughues_palms','palm_straight.obj')),('PalmBent',os.path.join(source,'yughues_palms','palm_bend.obj')),('Fern',os.path.join(source,'fern_02','fern_02_2k.fbx'))]:
    before = set(scene.objects)
    if path.endswith('.obj'):
        bpy.ops.wm.obj_import(filepath=path)
    else:
        bpy.ops.import_scene.fbx(filepath=path)
    imported = [o for o in scene.objects if o not in before and o.type=='MESH']
    chosen = imported[:1]
    obj = chosen[0]
    matrix = obj.matrix_world.copy()
    for v in obj.data.vertices:
        v.co = matrix @ v.co
    obj.matrix_world.identity()
    minimum = Vector(tuple(min(v.co[i] for v in obj.data.vertices) for i in range(3)))
    maximum = Vector(tuple(max(v.co[i] for v in obj.data.vertices) for i in range(3)))
    center = (minimum+maximum)*.5
    center.z = minimum.z
    size = maximum-minimum
    if key != 'Fern':
        bottom = [v.co for v in obj.data.vertices if v.co.z < minimum.z+size.z*.025]
        center.x = sum(v.x for v in bottom)/len(bottom)
        center.y = sum(v.y for v in bottom)/len(bottom)
    scale = 1/max(size.x,size.y) if key=='Fern' else 1/size.z
    for v in obj.data.vertices:
        v.co = (v.co-center)*scale
    obj.data.materials.clear()
    obj.data.materials.append(materials['CoastalFern' if key=='Fern' else 'CoastalPalm'])
    for p in obj.data.polygons:
        p.material_index = 0
    obj.name = 'Template_'+key
    templates[key] = obj
    obj.hide_set(True)
    obj.hide_render = True
    for other in imported[1:]:
        bpy.data.objects.remove(other,do_unlink=True)

def palm(name, height, bend=0):
    template = templates['PalmBent' if abs(bend) > .5 else 'Palm']
    obj = template.copy()
    obj.data = template.data.copy()
    obj.name = name+'_Palm'
    obj.scale = (height,height,height)
    obj.hide_render = False
    scene.collection.objects.link(obj)
    obj.hide_set(False)
    return [obj]

def fern(name, height=1.2):
    obj = templates['Fern'].copy()
    obj.data = templates['Fern'].data.copy()
    obj.name = name+'_Fern'
    obj.scale = (height,height,height)
    obj.hide_render = False
    scene.collection.objects.link(obj)
    obj.hide_set(False)
    return obj

def export_lods(name, objects):
    activate(objects)
    base_faces = sum(len(o.data.polygons) for o in objects)
    for level, ratio in enumerate((1, .28, .075)):
        active = []
        for obj in objects:
            copy = obj.copy()
            copy.data = obj.data.copy()
            scene.collection.objects.link(copy)
            copy.name = obj.name + '_LOD' + str(level)
            if level and len(copy.data.polygons) > 120:
                mod = copy.modifiers.new('CoastalLOD', 'DECIMATE')
                mod.ratio = ratio
                activate([copy])
                bpy.ops.object.modifier_apply(modifier=mod.name)
            active.append(copy)
        activate(active)
        bpy.ops.object.join()
        active = [bpy.context.object]
        active[0].name = name + '_LOD' + str(level)
        path = os.path.join(export, name + '_LOD' + str(level) + '.fbx')
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True, use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
        for obj in active:
            mesh = obj.data
            bpy.data.objects.remove(obj, do_unlink=True)
            bpy.data.meshes.remove(mesh)
    return base_faces

def fracture_blocks(bm, maximum, name):
    seen = set()
    components = []
    for vertex in bm.verts:
        if vertex in seen:
            continue
        component = {vertex}
        stack = [vertex]
        seen.add(vertex)
        while stack:
            current = stack.pop()
            for edge in current.link_edges:
                other = edge.other_vert(current)
                if other not in seen:
                    seen.add(other)
                    component.add(other)
                    stack.append(other)
        components.append(component)
    new_vertices, new_faces, tones = [], [], []
    rng = random.Random(name+'_Blocks')
    period = max(.65,min(8.5,maximum*.074))
    if not name.startswith(('Sea_', 'SeaArch_', 'Reef_')):
        period = max(1.5,maximum*.28)
    def clip(mesh, axis, value, lower):
        normal = Vector((0,0,0))
        normal[axis] = 1
        position = Vector((0,0,0))
        position[axis] = value
        cut = bmesh.ops.bisect_plane(mesh,geom=list(mesh.verts)+list(mesh.edges)+list(mesh.faces),dist=.00002,plane_co=position,plane_no=normal,clear_inner=lower,clear_outer=not lower)
        boundary = [edge for edge in cut['geom_cut'] if isinstance(edge,bmesh.types.BMEdge) and edge.is_boundary]
        if boundary:
            bmesh.ops.holes_fill(mesh,edges=boundary,sides=0)
    def append(mesh, tone):
        if len(mesh.faces)<3:
            return
        bmesh.ops.recalc_face_normals(mesh,faces=list(mesh.faces))
        bmesh.ops.dissolve_limit(mesh,angle_limit=.025,verts=list(mesh.verts),edges=list(mesh.edges),delimit={'NORMAL'})
        center = sum((v.co for v in mesh.verts),Vector())/max(1,len(mesh.verts))
        minimum = Vector(tuple(min(v.co[i] for v in mesh.verts) for i in range(3)))
        size = Vector(tuple(max(v.co[i] for v in mesh.verts) for i in range(3)))-minimum
        gap = min(1.1,maximum*.0075)
        for vertex in mesh.verts:
            for axis in range(3):
                vertex.co[axis] = center[axis]+(vertex.co[axis]-center[axis])*max(.9,1-gap/max(.1,size[axis]))
        sharp = [edge for edge in mesh.edges if len(edge.link_faces)==2 and edge.calc_face_angle()>.32]
        if sharp:
            bmesh.ops.bevel(mesh,geom=sharp,offset=min(.42,period*.10,max(.025,min(size)*.12)),segments=2,affect='EDGES',clamp_overlap=True)
        bmesh.ops.triangulate(mesh,faces=list(mesh.faces))
        mesh.verts.ensure_lookup_table()
        index = {v:i+len(new_vertices) for i,v in enumerate(mesh.verts)}
        new_vertices.extend(tuple(v.co) for v in mesh.verts)
        for face in mesh.faces:
            new_faces.append(tuple(index[v] for v in face.verts))
            tones.append(tone)
    for component in components:
        low = Vector(tuple(min(v.co[i] for v in component) for i in range(3)))
        high = Vector(tuple(max(v.co[i] for v in component) for i in range(3)))
        size = high-low
        if name == 'Reef_Spires_B' and low.z > -4 and size.z < maximum*.16 and Vector((low.x+high.x,low.y+high.y,0)).length*.5>maximum*.25:
            continue
        component_faces = {face for v in component for face in v.link_faces}
        src = bmesh.new()
        mapping = {v:src.verts.new(v.co) for v in component}
        for face in component_faces:
            src.faces.new([mapping[v] for v in face.verts])
        if size.z < period*.7 or max(size.x,size.y)<period*.8:
            append(src,0 if rng.random()>.22 else 3)
            src.free()
            continue
        axes = []
        for axis in (0,1):
            values = [low[axis]-.01]
            while values[-1] < high[axis]:
                values.append(values[-1]+period*rng.uniform(.74,1.23))
            axes.append(values)
        for xi in range(len(axes[0])-1):
            for yi in range(len(axes[1])-1):
                column = src.copy()
                for axis,values,i in [(0,axes[0],xi),(1,axes[1],yi)]:
                    clip(column,axis,values[i],True)
                    if not column.verts:
                        break
                    clip(column,axis,values[i+1],False)
                if len(column.faces)<3:
                    column.free()
                    continue
                zlow = min(v.co.z for v in column.verts)
                zhigh = max(v.co.z for v in column.verts)
                zvalues=[zlow-.01]
                while zvalues[-1] < zhigh:
                    zvalues.append(zvalues[-1]+period*rng.uniform(1.25,2.4))
                for zi in range(len(zvalues)-1):
                    block=column.copy()
                    clip(block,2,zvalues[zi],True)
                    if block.verts:
                        clip(block,2,zvalues[zi+1],False)
                    choice=rng.random()
                    tone=2 if choice>.80 else 3 if choice<.14 else 0
                    append(block,tone)
                    block.free()
                column.free()
        src.free()
    return new_vertices,new_faces,tones

reports = []
for record in baseline['prefabs']:
    name = record['name']
    if name in ('Palm', 'PalmBent', 'PalmYoung', 'Bush', 'Fern'):
        continue
    pieces = record['renders']
    vertices, faces = [], []
    for piece in pieces:
        offset = len(vertices)
        vertices.extend(tuple(convert(v)) for v in piece['vertices'])
        indices = piece['triangles']
        faces.extend(tuple(offset + indices[i + j] for j in range(3)) for i in range(0, len(indices), 3))
    obj = mesh_object(name + '_FracturedStone', vertices, faces, 'CoastalRock')
    obj.data.materials.append(materials['CoastalRockDark'])
    obj.data.materials.append(materials['CoastalRockLight'])
    obj.data.materials.append(materials['CoastalRockWarm'])
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0002)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bounds = [Vector(v) for v in vertices]
    maximum = max(max(v[i] for v in bounds) - min(v[i] for v in bounds) for i in range(3))
    fractured_vertices, fractured_faces, tones = fracture_blocks(bm,maximum,name)
    bm.free()
    obj.data.clear_geometry()
    obj.data.from_pydata(fractured_vertices,[],fractured_faces)
    obj.data.update()
    for face,tone in zip(obj.data.polygons,tones):
        face.material_index = tone
        face.use_smooth = False
    activate([obj])
    planar = obj.modifiers.new('RemoveCoplanarDetail','DECIMATE')
    planar.decimate_type = 'DISSOLVE'
    planar.angle_limit = .045
    planar.delimit = {'MATERIAL'}
    bpy.ops.object.modifier_apply(modifier=planar.name)
    obj.data.calc_loop_triangles()
    count = len(obj.data.loop_triangles)
    budget = 140000 if name == 'Sea_Lagoon_Cave' else 65000 if name == 'SeaArch_Huge_A' else 28000 if name.startswith('Reef_') else 5000 if name.startswith('CliffWall') else 2000
    if count > budget:
        reduction = obj.modifiers.new('CoastalDetailBudget','DECIMATE')
        reduction.ratio = budget/count
        bpy.ops.object.modifier_apply(modifier=reduction.name)
    triangulation = obj.modifiers.new('CoastalTriangles','TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=triangulation.name)
    objects = [obj]
    if name.startswith(('Sea_', 'SeaArch_', 'Reef_')) and name != 'Reef_ShallowField_A':
        bvh = BVHTree.FromPolygons([v.co for v in obj.data.vertices],[tuple(p.vertices) for p in obj.data.polygons],all_triangles=True)
        candidates = [p for p in obj.data.polygons if p.normal.z > .93 and p.center.z > 4]
        rng = random.Random(name)
        rng.shuffle(candidates)
        occupied = []
        for polygon in candidates:
            p = polygon.center.copy()
            support = min(2.8,maximum*.028)
            supported = True
            for dx,dy in [(0,0),(support,0),(-support,0),(0,support),(0,-support)]:
                hit,normal,index,distance = bvh.ray_cast(p+Vector((dx,dy,2.5)),Vector((0,0,-1)),5)
                if hit is None or normal.z < .75 or abs(hit.z-p.z) > 1.1:
                    supported=False
                    break
            if supported and all((p - other).length > maximum*.21 for other in occupied):
                height = rng.uniform(.13,.20)*maximum
                height = max(9,min(24,height))
                plants = palm(name + '_Palm' + str(len(occupied)), height, height * rng.uniform(-.14, .14))
                for plant in plants:
                    plant.location = p - Vector((0, 0, .2))
                objects.extend(plants)
                occupied.append(p)
                if len(occupied) >= (5 if maximum > 150 else 3):
                    break
        plant_positions = []
        for polygon in candidates:
            if any((polygon.center - position).length < 3.5 for position in plant_positions):
                continue
            plant = fern(name + '_CrevicePlant' + str(len(objects)), rng.uniform(.9, 2))
            plant.location = polygon.center - Vector((0, 0, .05))
            objects.append(plant)
            plant_positions.append(polygon.center.copy())
            if len(plant_positions) >= (18 if maximum > 150 else 10):
                break
    count = export_lods(name, objects)
    reports.append({'name': name, 'faces': count, 'meshes': len(objects), 'rootSpace': 'Unity metres Y-up', 'silhouette': 'original render envelope; relief recessed'})
    for obj in objects:
        obj.hide_set(True)

for name, height, bend in [('Palm', 9, 0), ('PalmBent', 10, 2.0), ('PalmYoung', 5, .25)]:
    if selection and name not in selection:
        continue
    objects = palm(name, height, bend)
    reports.append({'name': name, 'faces': export_lods(name, objects)})
    for obj in objects:
        obj.hide_set(True)
for name in ('Bush', 'Fern'):
    if selection and name not in selection:
        continue
    objects = [fern(name, 1.5 if name == 'Bush' else 1.1)]
    reports.append({'name': name, 'faces': export_lods(name, objects)})
    for obj in objects:
        obj.hide_set(True)

scene['Concept'] = 'C: vertical fractured Caribbean limestone blocks'
scene['Physics'] = 'No colliders exported. Original Unity collision and placement retained.'
scene['Sources'] = 'Poly Haven CC0; custom Blender geometry'
for mat in materials.values():
    for node in mat.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image is not None and node.image.packed_file is None:
            node.image.pack()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(project, 'Art', 'Blender', 'World', 'CoastalEnvironment', 'CoastalEnvironment.blend'), copy=True)
manifest_path = os.path.join(output, 'model-manifest.json')
if selection and os.path.exists(manifest_path):
    prior = json.load(open(manifest_path,encoding='utf-8'))
    replacement = {record['name']:record for record in reports}
    reports = [replacement.get(record['name'],record) for record in prior]
json.dump(reports, open(manifest_path, 'w', encoding='utf-8'), indent=2)
bpy.context.window.scene = previous_scene
result = {'models': len(reports), 'faces': sum(r['faces'] for r in reports), 'manifest': os.path.join(output, 'model-manifest.json'), 'saved': True}

