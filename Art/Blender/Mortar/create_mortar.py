import bpy, math, os
from mathutils import Vector
base = r'D:/projects/Pirate_BR/PirateGame'
test = bpy.data.objects.new('MortarConnectionCheck', None)
bpy.context.scene.collection.objects.link(test)
bpy.data.objects.remove(test, do_unlink=True)
scene = bpy.data.scenes.new('MortarAsset')
collection = scene.collection
objects = []
def xyz(p): return (p[0], -p[2], p[1])
def mesh(name, vertices, faces, material):
    data = bpy.data.meshes.new(name)
    data.from_pydata([xyz(v) for v in vertices], [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.data.materials.append(material)
    objects.append(obj)
    return obj
def mat(name, color):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    return m
iron=mat('MortarIron',(.055,.07,.08)); brass=mat('MortarBrass',(.55,.33,.1)); wood=mat('MortarWood',(.25,.11,.04)); bore=mat('MortarBore',(.009,.012,.014))
def box(name, p, s, material):
    verts=[(p[0]+x*s[0]/2,p[1]+y*s[1]/2,p[2]+z*s[2]/2) for x,y,z in [(-1,-1,-1),(-1,-1,1),(-1,1,1),(-1,1,-1),(1,-1,-1),(1,-1,1),(1,1,1),(1,1,-1)]]
    o=mesh(name,verts,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(0,3,7,4),(1,5,6,2)],material)
    bevel=o.modifiers.new('EdgeBevel','BEVEL');bevel.width=.035;bevel.segments=1
    return o
def tube(name, rings, material):
    n=16;v=[];f=[]
    for z,r in rings:
        for i in range(n):
            a=i*math.tau/n;v.append((math.cos(a)*r,.85+math.sin(a)*r,z))
    for j in range(len(rings)-1):
        for i in range(n):
            a=j*n+i;b=j*n+(i+1)%n;f.append((a,b,b+n,a+n))
    return mesh(name,v,f,material)
for x in [-.62,.62]:
    box('BaseRunner',(x,.16,0),(.32,.32,1.65),wood)
    box('IronStrap',(x,.335,-.55),(.36,.08,.18),iron)
    box('IronStrap',(x,.335,.55),(.36,.08,.18),iron)
    box('TrunnionSupport',(x,.57,-.12),(.23,.67,.62),wood)
    box('BearingCap',(x,.91,-.12),(.3,.14,.55),brass)
for z in [-.6,.55]: box('CrossBeam',(0,.27,z),(1.5,.22,.25),wood)
tube('Barrel', [(-.48,.02),(-.48,.34),(-.3,.49),(.45,.46),(.85,.5),(.97,.5),(.97,.34),(.2,.29),(-.25,.02)],iron)
tube('BarrelMuzzleBand',[(.78,.505),(.86,.54),(.97,.54),(1.01,.5),(1.01,.34),(.97,.34)],brass)
tube('BarrelBreechBand',[(-.32,.49),(-.23,.515),(-.13,.515),(-.08,.49)],brass)
tube('BarrelBore',[(-.24,.025),(.15,.29),(.88,.335)],bore)
for i,p in enumerate([(0,0,0),(1,0,0),(0,1,0),(0,0,1)]):
    o=bpy.data.objects.new(['Anchor_Origin','Anchor_Right','Anchor_Up','Anchor_Forward'][i],None);collection.objects.link(o);o.location=xyz(p);objects.append(o)
old=bpy.context.window.scene
bpy.context.window.scene=scene
try:
    for o in scene.objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.wm.save_as_mainfile(filepath=base+'/Art/Blender/Mortar/ShipMortar.blend',copy=True)
    bpy.ops.export_scene.fbx(filepath=base+'/Assets/Models/Mortar/ShipMortar.fbx',use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False)
finally:
    bpy.context.window.scene=old
result={'objects':len(objects),'export':'Assets/Models/Mortar/ShipMortar.fbx'}
