import bpy
import math
import os
from mathutils import Vector

ROOT = "D:/projects/Pirate_BR/PirateGame"
probe = bpy.data.objects.new("SailRiggingConnectionProbe", None)
bpy.context.scene.collection.objects.link(probe)
probe.location = (1, 2, 3)
assert tuple(probe.location) == (1, 2, 3)
bpy.data.objects.remove(probe, do_unlink=True)
scene = bpy.data.scenes.get("SailRiggingWorkshop") or bpy.data.scenes.new("SailRiggingWorkshop")
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
collection = bpy.data.collections.get("SailRigging") or bpy.data.collections.new("SailRigging")
if collection.name not in scene.collection.children:
    scene.collection.children.link(collection)
parts = []

def material(name, color, metal=0, rough=.65):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    return mat

wood = material("Rigging_ShipWood", (.32, .12, .045))
image = bpy.data.images.load(ROOT + "/Assets/Models/Ships/MainShip/Textures/StylShip_Masts_BaseColor.png", check_existing=True)
texture = wood.node_tree.nodes.new("ShaderNodeTexImage")
texture.image = image
wood.node_tree.links.new(texture.outputs["Color"], next(n for n in wood.node_tree.nodes if n.type == 'BSDF_PRINCIPLED').inputs["Base Color"])
iron = material("Rigging_BlackenedIron", (.10, .135, .145), .72, .48)
iron_edge = material("Rigging_IronEdges", (.20, .245, .25), .65, .43)
brass = material("Rigging_OldBrass", (.46, .30, .10), .65, .5)
hemp = material("Rigging_Hemp", (.48, .36, .20), 0, .95)
hemp_light = material("Rigging_HempLight", (.59, .46, .28), 0, .95)
dark = material("Rigging_Recess", (.04, .028, .016), 0, 1)

def register(obj, name, mat, group):
    obj.name = name
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    collection.objects.link(obj)
    if mat:
        obj.data.materials.append(mat)
    obj["export_group"] = group
    parts.append(obj)
    return obj

def uv_wood(obj):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    coords = [v.co for v in mesh.vertices]
    mins = [min(v[i] for v in coords) for i in range(3)]
    spans = [max(v[i] for v in coords) - mins[i] for i in range(3)]
    long_axis = max(range(3), key=lambda i: spans[i])
    for poly in mesh.polygons:
        face_axis = max(range(3), key=lambda i: abs(poly.normal[i]))
        axes = [i for i in range(3) if i != face_axis]
        vertical = long_axis if long_axis in axes else axes[1]
        horizontal = next(i for i in axes if i != vertical)
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            u = (co[horizontal] - mins[horizontal]) / max(spans[horizontal], .001)
            v = (co[vertical] - mins[vertical]) / max(spans[vertical], .001)
            mesh.uv_layers.active.data[li].uv = (.406 + u * .07, .15 + v * .35)

def bevel(obj, amount=.012):
    mod = obj.modifiers.new("HandFinishedEdges", 'BEVEL')
    mod.width = amount
    mod.segments = 2
    mod.affect = 'EDGES'
    mod = obj.modifiers.new("WeightedCornerNormals", 'WEIGHTED_NORMAL')
    mod.keep_sharp = True

def box(name, pos, size, mat=wood, group="Rack", edge=.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = register(bpy.context.object, name, mat, group)
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if mat == wood:
        uv_wood(obj)
    if edge:
        bevel(obj, edge)
    return obj

def cylinder(name, pos, radius, length, mat, group="Rack", axis=(0,0,1), sides=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=length, location=pos)
    obj = register(bpy.context.object, name, mat, group)
    obj.rotation_euler = Vector(axis).to_track_quat('Z', 'Y').to_euler()
    if mat == wood:
        uv_wood(obj)
    bevel(obj, min(.005, radius * .1))
    return obj

def lathe(name, pos, profile, mat=wood, group="Rack", axis=(0,0,1), sides=12):
    verts, faces = [], []
    for z, radius in profile:
        for i in range(sides):
            a = i * math.tau / sides
            verts.append((math.cos(a)*radius, math.sin(a)*radius, z))
    for j in range(len(profile)-1):
        for i in range(sides):
            n = j*sides+i
            faces.append((n, j*sides+(i+1)%sides, (j+1)*sides+(i+1)%sides, n+sides))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple((len(profile)-1)*sides+i for i in range(sides)))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    collection.objects.link(obj)
    register(obj,name,mat,group)
    obj.location=pos
    obj.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler()
    if mat == wood:
        uv_wood(obj)
    bevel(obj,.003)
    return obj

def tube(name, points, radius, mat=hemp, group="Rack", sides=6):
    verts, faces=[],[]
    for j,p in enumerate(points):
        p=Vector(p)
        tangent=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(j-1,0)])
        tangent.normalize()
        n=tangent.cross(Vector((0,0,1)))
        if n.length < .01:
            n=tangent.cross(Vector((1,0,0)))
        n.normalize()
        b=tangent.cross(n).normalized()
        for i in range(sides):
            a=math.tau*i/sides
            verts.append(p + radius*(math.cos(a)*n+math.sin(a)*b))
    for j in range(len(points)-1):
        for i in range(sides):
            a=j*sides+i
            faces.append((a,j*sides+(i+1)%sides,(j+1)*sides+(i+1)%sides,a+sides))
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    collection.objects.link(obj)
    register(obj,name,mat,group)
    return obj

def bolt(name, x, y, z, group="Rack"):
    cylinder(name+"_Washer",(x,y,z),.029,.012,iron_edge,group,(0,1,0))
    cylinder(name+"_Head",(x,y+.011,z),.018,.016,brass,group,(0,1,0),6)

for side in (-1,1):
    x=side*1.08
    box("Upright", (x,0,1.02), (.19,.23,2.04), edge=.023)
    box("Foot", (x,0,.07), (.38,.58,.14), iron, edge=.022)
    box("PostCap", (x,0,2.11), (.27,.29,.09), wood, edge=.025)
    for z in (.27,.59,1.76,1.94):
        box("IronStrap",(x,0,z),(.207,.246,.075),iron,edge=.008)
        bolt("StrapRivet",x,.127,z)
    for dx in (-.115,.115):
        cylinder("DeckBolt",(x+dx,.17,.147),.025,.017,iron_edge,sides=6)
    brace=box("KneeBrace",(x,-.11,.45),(.115,.14,.74),wood,edge=.012)
    brace.rotation_euler.x=side*0+.55
    box("BraceFoot",(x,-.29,.12),(.20,.26,.10),iron,edge=.015)

box("CrownBeam",(0,0,1.96),(2.42,.25,.24),wood,edge=.026)
box("BelayingRail",(0,.015,.57),(2.37,.31,.17),wood,edge=.018)
box("BackApron",(0,-.08,.37),(2.15,.075,.22),wood,edge=.012)
for x in (-1.08,1.08):
    bolt("BeamBolt",x,.14,1.96)
    bolt("RailBolt",x,.19,.57)
for i in range(4):
    x=-.78+i*.52
    label=box("NumberPlate_"+str(i),(x,.14,1.97),(.18,.025,.11),brass,edge=.009)
    for mark in range(i+1):
        box("NumberInlay",(x+(mark-i*.5)*.026,.157,1.97),(.008,.007,.059),dark,edge=.001)
    cylinder("BlockAxle",(x,0,1.79),.028,.20,iron,axis=(1,0,0))
    for side in (-1,1):
        cheek=box("PulleyCheek",(x+side*.068,0,1.78),(.055,.22,.31),wood,edge=.036)
        box("BlockBinding",(x+side*.096,0,1.78),(.017,.16,.18),iron,edge=.022)
    cylinder("Sheave",(x,0,1.79),.094,.075,brass,axis=(1,0,0),sides=16)
    for side in (-1,1):
        cylinder("AxleCap",(x+side*.112,0,1.79),.033,.022,iron_edge,axis=(1,0,0),sides=8)
    lathe("BelayingPin",(x,.105,.56),[(-.14,.018),(-.08,.025),(.12,.027),(.14,.047),(.21,.043),(.25,.025)],wood)
    for turn in range(4):
        points=[]
        for j in range(65):
            a=j*math.tau/64
            points.append((x+(.095+turn*.006)*math.cos(a), .17+turn*.019, .40+.15*math.sin(a)))
        tube("HangingCoil",points,.014,hemp if turn%2==0 else hemp_light)
    for turn in range(3):
        points=[]
        for j in range(33):
            a=j*math.tau/32
            points.append((x+.045*math.cos(a),.105+.045*math.sin(a),.66+turn*.021))
        tube("PinTurns",points,.012)

profile=[(-.175,.045),(-.16,.058),(-.13,.06),(-.115,.045),(-.05,.038),(0,.034),(.05,.038),(.115,.045),(.13,.06),(.16,.058),(.175,.045)]
lathe("TurnedGrip",(0,0,0),profile,wood,"Grip",(1,0,0),16)
for x in (-.145,.145):
    cylinder("GripFerrule",(x,0,0),.061,.029,iron,"Grip",(1,0,0))
    cylinder("GripEndCap",(math.copysign(.179,x),0,0),.041,.009,brass,"Grip",(1,0,0))
for i in range(5):
    points=[]
    for j in range(33):
        a=j*math.tau/32
        points.append((-.034+i*.017,.043*math.cos(a),.043*math.sin(a)))
    tube("GripLashing",points,.009,hemp,"Grip")
points=[(.045*math.sin(j*math.tau/48),0,.075+.06*math.cos(j*math.tau/48)) for j in range(49)]
tube("RopeEye",points,.012,hemp,"Grip")

for strand in range(3):
    points=[]
    for j in range(181):
        z=j/180
        a=z*math.tau/.12+strand*math.tau/3
        points.append((math.cos(a)*.010,math.sin(a)*.010,z))
    tube("LaidHempSample",points,.009,hemp if strand!=1 else hemp_light,"RopeSample")

for group in ("Rack","Grip","RopeSample"):
    selected=[o for o in parts if o.get("export_group")==group]
    bpy.ops.object.select_all(action='DESELECT')
    for o in selected:
        o.select_set(True)
    bpy.context.view_layer.objects.active=selected[0]
    path=ROOT+"/Assets/Models/SailRigging"
    os.makedirs(path,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path+"/Sail"+group+".fbx",use_selection=True,object_types={'MESH'},use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS')
bpy.ops.object.select_all(action='DESELECT')
for o in parts:
    if o.get("export_group")=="Rack":
        o.select_set(True)
    elif o.get("export_group")=="Grip":
        o.location += Vector((-.78,.21,1.65))
    else:
        o.hide_set(True)
image.pack()
os.makedirs(ROOT+"/Art/Blender/SailRigging",exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+"/Art/Blender/SailRigging/SailRigging.blend")
result={"version":bpy.app.version_string,"objects":len(parts),"files":["SailRack.fbx","SailGrip.fbx","SailRopeSample.fbx"],"source":bpy.data.filepath}
