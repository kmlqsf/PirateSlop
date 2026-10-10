import bpy, math, os, shutil, numpy as np
from mathutils import Vector, Matrix
project = r'C:\Users\K\Project'
previous = bpy.context.window.scene
for source_name, source_file in [('FishingRodReplacement','FishingRodReplacement.blend'),('FishingReelSources','FishingReelSources.blend')]:
    if bpy.data.scenes.get(source_name) is None:
        with bpy.data.libraries.load(os.path.join(project,'Art','Blender','FishingRod',source_file),link=False) as (source_data,target_data):
            target_data.scenes=[source_name]
existing = bpy.data.scenes.get('FishingRodAssembly')
if existing is not None:
    for old in list(existing.objects): bpy.data.objects.remove(old, do_unlink=True)
    bpy.data.scenes.remove(existing)
scene = bpy.data.scenes.new('FishingRodAssembly')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1

def b(point):
    return Vector((point[0], -point[2], point[1]))

def empty(name, point=(0,0,0), parent=None):
    o = bpy.data.objects.new(name, None)
    scene.collection.objects.link(o)
    o.parent = parent
    o.location = b(point)
    return o

root = empty('FishingRodAssembly')
rod_source = bpy.data.scenes['FishingRodReplacement'].objects['FishingRodGeometry']
rod = rod_source.copy()
rod.data = rod_source.data.copy()
rod.name = 'FishingRodGeometry'
scene.collection.objects.link(rod)
rod.parent = root
sources = bpy.data.scenes['FishingReelSources']
texture_folder = os.path.join(project, 'Assets', 'Models', 'Fishing', 'Replacement', 'Textures')

def copy_maps(material, key):
    material.name = key
    images = [n.image for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
    for image in images:
        suffix = next((s for s in ['basecolor','normal','metallic','roughness'] if s in image.name.lower()), None)
        if suffix is None: continue
        filename = key + {'basecolor':'BaseColor.jpg','normal':'Normal.png','metallic':'Metallic.jpg','roughness':'Roughness.jpg'}[suffix]
        destination = os.path.join(texture_folder, filename)
        source_path=bpy.path.abspath(image.filepath)
        if not os.path.exists(source_path) and image.packed_file:
            with open(destination,'wb') as output: output.write(image.packed_file.data)
        elif os.path.normcase(os.path.abspath(source_path)) != os.path.normcase(os.path.abspath(destination)):
            shutil.copyfile(source_path, destination)
        image.filepath = destination
        if not image.packed_file: image.pack()
    metal = next(i for i in images if 'metallic' in i.name.lower())
    rough = next(i for i in images if 'roughness' in i.name.lower())
    n = metal.size[0] * metal.size[1]
    m = np.empty(n*4,dtype=np.float32); r = np.empty(n*4,dtype=np.float32)
    metal.pixels.foreach_get(m); rough.pixels.foreach_get(r)
    pixels = np.zeros((n,4),dtype=np.float32)
    pixels[:,0] = m.reshape(-1,4)[:,0]; pixels[:,3] = 1-r.reshape(-1,4)[:,0]
    image = bpy.data.images.new(key+'MetalSmooth', width=metal.size[0], height=metal.size[1], alpha=True)
    image.colorspace_settings.name='Non-Color'; image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw=os.path.join(texture_folder,key+'MetalSmooth.png'); image.file_format='PNG'; image.save()
    return material

mount_source = sources.objects['ReelMountSource']
mount = mount_source.copy(); mount.data = mount_source.data.copy()
mount.name = 'ReelMountGeometry'; scene.collection.objects.link(mount); mount.parent = root
mount.data.materials[0] = copy_maps(mount.data.materials[0].copy(), 'ReelMount')
for v, old in zip(mount.data.vertices, mount_source.data.vertices):
    p=mount_source.matrix_world@old.co
    canonical=Vector((.058-(p.x-.45)*.25, .07+(p.z-.23)*.25, .18+p.y*.25))
    v.co=b(canonical)
mount.matrix_basis=Matrix.Identity(4)
spool_center=(.20,.07,.18)
spool_pivot = empty('ReelSpoolPivot', spool_center, root)
spool_source = sources.objects['ReelMechanismSource']
spool = spool_source.copy(); spool.data = spool_source.data.copy()
spool.name='ReelSpoolGeometry'; scene.collection.objects.link(spool); spool.parent=spool_pivot
spool.matrix_basis=Matrix.Identity(4)
spool.data.materials[0] = copy_maps(spool.data.materials[0].copy(), 'ReelMechanism')
for v,old in zip(spool.data.vertices,spool_source.data.vertices):
    p=spool_source.matrix_world@old.co
    v.co=b((p.x*.21,(p.z-.486)*.21,-(p.y+.00085)*.21))

def mat(name,color,metal=0,rough=.5):
    m=bpy.data.materials.new(name); m.use_nodes=True
    shader=m.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=(*color,1)
    shader.inputs['Metallic'].default_value=metal; shader.inputs['Roughness'].default_value=rough
    return m
brass=mat('ReelBrass',(.37,.21,.075),.7,.34)
thread=mat('ReelThread',(.43,.48,.42),0,.8)
wood=mat('ReelGrip',(.075,.026,.012),0,.6)

def tube(name,points,radius,material,parent=root,cyclic=False):
    curve=bpy.data.curves.new(name,'CURVE'); curve.dimensions='3D'; curve.resolution_u=1
    curve.bevel_depth=radius; curve.bevel_resolution=1; curve.resolution_u=1; curve.use_fill_caps=not cyclic
    spline=curve.splines.new('POLY'); spline.points.add(len(points)-1)
    for item,p in zip(spline.points,points):item.co=(*b(p),1)
    spline.use_cyclic_u=cyclic
    o=bpy.data.objects.new(name,curve); scene.collection.objects.link(o); o.parent=parent
    curve.materials.append(material)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH')
    for face in o.data.polygons:face.use_smooth=True
    return o

helix=[]
for i in range(24*24+1):
    t=i/(24*24);angle=t*math.pi*48
    helix.append((-.022+.034*t,.0715*math.cos(angle),.0715*math.sin(angle)))
tube('ReelWinding',helix,.0016,thread,spool_pivot)
tube('ReelCrankArm',[(.073,0,0),(.078,-.05,.04)],.005,brass,spool_pivot)
tube('ReelCrankGrip',[(.078,-.05,.04),(.112,-.05,.04)],.009,wood,spool_pivot)
tube('ReelCrankHub',[(.06,0,0),(.077,0,0)],.012,brass,spool_pivot)
guide_points = [(.055,-.020,.49),(.073,.084,.905),(.071,.140,1.15),(.058,.157,1.38),(.034,.164,1.53),(0,.133,1.69)]
for index,point in enumerate(guide_points): empty('LineGuide_'+str(index),point,root)
empty('ReelLineExit',(.20,-.003,.18),root)
preview=[(.20,-.003,.18)]+guide_points
tube('AssemblyLinePreview',preview,.0011,thread)
bpy.context.view_layer.update()
export_objects=[o for o in scene.objects if o.name!='AssemblyLinePreview']
bpy.ops.object.select_all(action='DESELECT')
for o in export_objects:o.select_set(True)
bpy.context.view_layer.objects.active=root
export=os.path.join(project,'Assets','Models','Fishing','Replacement','FishingRodReplacement.fbx')
bpy.ops.export_scene.fbx(filepath=export,use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,bake_anim=False,path_mode='STRIP',use_mesh_modifiers=True)
bpy.data.libraries.write(os.path.join(project,'Art','Blender','FishingRod','FishingRodAssembly.blend'),{scene},fake_user=True,compress=True)
bpy.context.window.scene=previous
print({'objects':len(scene.objects),'guides':guide_points,'spool_center':spool_center,'export':export})
