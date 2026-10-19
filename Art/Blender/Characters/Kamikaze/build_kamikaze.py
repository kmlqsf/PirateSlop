import bpy, math, json, os, random
from mathutils import Vector, Matrix

BASE = r'C:\Users\K\Project'
SOURCE = os.path.join(BASE, 'Art/Blender/Characters/Kamikaze')
EXPORT = os.path.join(BASE, 'Assets/Models/Characters/Kamikaze')
PREVIEW = r'C:\Users\K\Documents\Codex\2026-10-19\new-chat\outputs'
random.seed(41)
old_scene = bpy.data.scenes.get('Scene', bpy.context.scene)
scene = bpy.data.scenes.get('KamikazeAsset')
if scene:
    for o in list(scene.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.scenes.remove(scene)
active_materials={m for ob in bpy.data.objects if ob.type=='MESH' for m in ob.data.materials if m}
for material in list(bpy.data.materials):
    if material.name.startswith(('KamikazePalette_','KamikazeAtlas')) and material not in active_materials:bpy.data.materials.remove(material,do_unlink=True)
for image in list(bpy.data.images):
    if image.name.startswith('KamikazePalette') and image.users==0:bpy.data.images.remove(image)
for skeleton in list(bpy.data.armatures):
    if skeleton.name.startswith('KamikazeSkeleton') and skeleton.users==0:bpy.data.armatures.remove(skeleton)
scene = bpy.data.scenes.new('KamikazeAsset')
for actionname in ['KamikazeIdle','KamikazeRun']:
    previous=bpy.data.actions.get(actionname)
    if previous:bpy.data.actions.remove(previous)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.render.resolution_x = 1100
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.fps = 30
scene.world = bpy.data.worlds.new('KamikazeStudioWorld')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.32,.34,.38,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .5
asset = bpy.data.collections.new('Kamikaze_GameAsset')
studio = bpy.data.collections.new('Kamikaze_Studio')
scene.collection.children.link(asset)
scene.collection.children.link(studio)
parts = []
skinparts = []
palette = [(0.57,.29,.12,1),(.65,.35,.16,1),(.72,.41,.20,1),(.61,.32,.14,1),(.15,.17,.075,1),(.20,.215,.10,1),(.245,.25,.13,1),(.18,.19,.085,1),(.105,.053,.030,1),(.16,.083,.045,1),(.235,.125,.065,1),(.035,.022,.014,1),(.001,.002,.004,1),(.10,.12,.15,1),(.39,.41,.43,1),(.55,.39,.19,1)]
mats=[]
for i,col in enumerate(palette):
    m=bpy.data.materials.new('KamikazePalette_%02d'%i)
    m.diffuse_color=col
    mats.append(m)

def link(obj):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    asset.objects.link(obj)

def finish(obj,name,col,bone=None):
    obj.name=name
    link(obj)
    obj.data.materials.clear()
    obj.data.materials.append(mats[col])
    if bone:
        obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))),1,'REPLACE')
    parts.append(obj)
    return obj

def ell(name,loc,scale,col=1,bone=None,segments=20,rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=1,location=loc)
    o=bpy.context.object
    o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,col,bone)

def mesh(name,verts,faces,col,bone=None):
    me=bpy.data.meshes.new(name)
    me.from_pydata(verts,[],faces)
    me.update()
    ob=bpy.data.objects.new(name,me)
    asset.objects.link(ob)
    return finish(ob,name,col,bone)

def tube(name,points,radii,col,bone=None,n=12):
    vs=[]; fs=[]
    for j,p in enumerate(points):
        p=Vector(p)
        tangent=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])
        tangent.normalize()
        side=tangent.cross(Vector((0,1,0)))
        if side.length<.01: side=tangent.cross(Vector((1,0,0)))
        side.normalize(); other=tangent.cross(side).normalized()
        rx,ry=radii[j] if isinstance(radii[j],tuple) else (radii[j],radii[j])
        for k in range(n):
            a=k*math.tau/n
            vs.append(tuple(p+side*(math.cos(a)*rx)+other*(math.sin(a)*ry)))
    for j in range(len(points)-1):
        for k in range(n): fs.append((j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k))
    fs.append(tuple(reversed(range(n))));fs.append(tuple((len(points)-1)*n+k for k in range(n)))
    return mesh(name,vs,fs,col,bone)

def box(name,loc,size,col,bone=None,bevel=.01):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object;o.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('TailoredEdges','BEVEL');mod.width=bevel;mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o,name,col,bone)

def join(objects,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    o=bpy.context.object;o.name=name
    for ob in objects:
        if ob in parts:parts.remove(ob)
    parts.append(o)
    return o

def union(objects,name,voxel,ratio):
    o=join(objects,name)
    m=o.modifiers.new('ConnectedAnatomy','REMESH');m.mode='VOXEL';m.voxel_size=voxel;m.use_smooth_shade=False
    bpy.ops.object.modifier_apply(modifier=m.name)
    m=o.modifiers.new('AnatomyRelax','SMOOTH');m.factor=1.1;m.iterations=3
    bpy.ops.object.modifier_apply(modifier=m.name)
    m=o.modifiers.new('DesignedFacets','DECIMATE');m.ratio=ratio
    bpy.ops.object.modifier_apply(modifier=m.name)
    return o

torso=[]
levels=[(1.04,.205,.13),(1.11,.215,.14),(1.20,.225,.145),(1.31,.265,.17),(1.43,.325,.185),(1.55,.355,.19),(1.64,.32,.17),(1.70,.23,.125),(1.76,.105,.09),(1.79,.09,.085)]
anchors=levels[:];levels=[]
for i in range(32):
    z=1.04+.75*i/31
    for a,b in zip(anchors,anchors[1:]):
        if a[0]<=z<=b[0]+1e-6:
            t=(z-a[0])/(b[0]-a[0]);levels.append((z,a[1]*(1-t)+b[1]*t,a[2]*(1-t)+b[2]*t));break
vs=[];fs=[];N=40
for z,w,d in levels:
    for k in range(N):
        a=math.tau*k/N;x=w*math.cos(a);y=d*math.sin(a)
        if y<0:
            front=(-math.sin(a))**2
            pec=.075*math.exp(-((z-1.51)/.13)**2)*math.exp(-((abs(x)-.165)/.14)**2)
            absb=sum(.048*math.exp(-((z-h)/.039)**2)*math.exp(-((abs(x)-.077)/.068)**2) for h in [1.185,1.285,1.385])
            groove=.016*math.exp(-(x/.025)**2)*math.exp(-((z-1.47)/.18)**2)
            y-=(pec+absb-groove)*front
        vs.append((x,y,z))
for j in range(len(levels)-1):
    for k in range(N):fs.append((j*N+k,j*N+(k+1)%N,(j+1)*N+(k+1)%N,(j+1)*N+k))
fs.extend([tuple(reversed(range(N))),tuple((len(levels)-1)*N+k for k in range(N))])
torso.append(mesh('TorsoAnatomy',vs,fs,1))
for s in [-1,1]:
    torso.append(ell('Pectoral',(.165*s,-.155,1.515),(.207,.105,.13),2))
    torso.append(ell('Trapezius',(.155*s,.025,1.665),(.16,.13,.07),1))
    torso.append(tube('ArmContinuous',[(.30*s,0,1.58),(.43*s,0,1.55),(.55*s,0,1.445),(.66*s,0,1.405),(.75*s,0,1.52),(.82*s,0,1.675),(.86*s,0,1.775)],[(.10,.11),(.14,.14),(.14,.125),(.105,.11),(.13,.11),(.10,.09),(.073,.074)],1,n=16))
    torso.append(ell('Deltoid',(.385*s,0,1.61),(.15,.15,.135),2))
    o=ell('Biceps',(.525*s,-.035,1.49),(.16,.13,.105),2);o.rotation_euler[1]=s*.55;torso.append(o)
    torso.append(ell('Triceps',(.505*s,.055,1.48),(.15,.105,.11),1))
body=union(torso,'Kamikaze_Skin',.013,.065)
body.data.materials.clear()
for m in mats:body.data.materials.append(m)
for p in body.data.polygons:
    center=p.center
    p.material_index=1

pants=[]
pants.append(ell('CargoPelvis',(0,0,1.005),(.255,.16,.16),5,bone='Hips'))
for s,side in [(-1,'R'),(1,'L')]:
    pants.append(tube('CargoLeg',[(.13*s,0,1.05),(.175*s,0,.93),(.205*s,0,.78),(.22*s,-.01,.66),(.22*s,.015,.59),(.23*s,-.012,.545),(.235*s,.01,.50),(.235*s,0,.42),(.235*s,-.012,.35),(.235*s,.012,.31),(.235*s,-.012,.27),(.235*s,0,.24)],[(.145,.16),(.165,.17),(.155,.16),(.14,.15),(.115,.13),(.14,.15),(.125,.12),(.14,.14),(.12,.11),(.135,.12),(.11,.105),(.10,.10)],5,n=16))
pantsob=union(pants,'Kamikaze_Cargo',.015,.09)
pantsob.data.materials.clear()
for m in mats:pantsob.data.materials.append(m)
for p in pantsob.data.polygons:p.material_index=5

for s,side in [(-1,'R'),(1,'L')]:
    thigh='Thigh_'+side;calf='Calf_'+side;foot='Foot_'+side;hand='Hand_'+side;fore='Forearm_'+side
    pocket=box('CargoPocket_'+side,(s*.345,-.015,.79),(.075,.205,.215),5,thigh,.018)
    flap=box('CargoPocketFlap_'+side,(s*.36,-.015,.89),(.085,.22,.052),6,thigh,.012)
    flap.rotation_euler[0]=.13;flap.rotation_euler[1]=s*.16
    ell('PocketStud_'+side,(s*.398,-.075,.882),(.006,.015,.015),13,thigh,12,6)
    bootlevels=[(.015,.125,.215,-.08),(.055,.128,.215,-.08),(.085,.12,.202,-.08),(.15,.112,.19,-.065),(.205,.093,.11,-.005),(.32,.085,.085,.015)]
    bv=[];bf=[];BN=12
    for z,w,d,yc in bootlevels:
        for k in range(BN):
            a=math.tau*k/BN
            bv.append((s*.235+w*math.cos(a),yc+d*math.sin(a),z))
    for j in range(len(bootlevels)-1):
        for k in range(BN):bf.append((j*BN+k,j*BN+(k+1)%BN,(j+1)*BN+(k+1)%BN,(j+1)*BN+k))
    bf.extend([tuple(reversed(range(BN))),tuple((len(bootlevels)-1)*BN+k for k in range(BN))])
    boot=mesh('HeavyBoot_'+side,bv,bf,9,foot)
    boot.data.materials.append(mats[11])
    for p in boot.data.polygons:
        if p.center.z<.07:p.material_index=1
    for z,rx,ry,yc in [(.13,.116,.195,-.065),(.19,.098,.125,-.008),(.25,.090,.092,.012),(.305,.088,.088,.015)]:
        sv=[];sf=[]
        for zz in [z-.013,z+.013]:
            for j in range(11):
                a=math.pi+math.pi*j/10
                sv.append((s*.235+rx*math.cos(a),yc+ry*math.sin(a),zz))
        for j in range(10):sf.append((j,j+1,j+12,j+11))
        mesh('BootStrap_'+side,sv,sf,10,foot)
        a=math.pi*1.72
        box('BootBuckle_'+side,(s*.235+rx*math.cos(a),yc+ry*math.sin(a)-.005,z),(.024,.008,.026),13,foot,.002)
    box('BootHeel_'+side,(s*.235,.055,.025),(.20,.14,.045),11,foot,.005)
    tube('WristCuff_'+side,[(s*.835,0,1.70),(s*.866,0,1.785)],[.091,.083],8,fore,n=12)
    tube('GloveBridge_'+side,[(s*.866,0,1.775),(s*.90,-.08,1.80),(s*.925,-.13,1.82)],[.075,.075,.06],8,hand,n=12)
    ell('GlovePalm_'+side,(s*.925,-.125,1.81),(.104,.044,.07),8,hand,16,10)
    for j in range(4):
        xx=.87+j*.034;endz=[1.952,1.987,1.974,1.938][j];fingerpoints=[]
        for u in [0,.35,.72,1]:
            z=1.825+(endz-1.825)*u;x=xx+[-.015,.005,.018,.032][j]*u
            y=-math.sqrt(max(.003,.22**2-(x-.98)**2-(z-1.985)**2))-.009
            fingerpoints.append((s*x,y,z))
        tube('GrippingFinger_'+side,fingerpoints,[.018,.019,.017,.014],1,hand,n=8)
    tube('Thumb_'+side,[(s*1.00,-.12,1.80),(s*1.08,-.16,1.84),(s*1.13,-.16,1.90),(s*1.13,-.18,1.94)],[.026,.025,.022,.017],1,hand,n=10)
    bomb='BombSocket_'+side
    bc=(s*.98,0,1.985)
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=.22,location=bc)
    finish(bpy.context.object,'Bomb_'+side,12,bomb)
    tube('BombFuseCollar_'+side,[(s*.98,0,2.19),(s*.98,0,2.23)],[.047,.042],13,bomb,n=12)
    pts=[(s*.98,0,2.225),(s*.99,0,2.26),(s*1.01,0,2.29),(s*1.025,0,2.31)]
    tube('BombFuse_'+side,pts,[.015,.015,.014,.012],15,bomb,n=8)
    for j in range(6):
        ell('FuseTwist_'+side,(s*(.985+j*.006),-.001,2.235+j*.012),(.018,.016,.008),15,bomb,8,4)

tube('LeatherBelt',[(0,0,1.055),(0,0,1.13)],[(.238,.157),(.238,.157)],8,'Hips',n=32)
for x in [-.2,-.13,.12,.20]:
    y=-.157*math.sqrt(1-(x/.238)**2)-.011
    box('BeltLoop',(x,y,1.092),(.028,.021,.079),5,'Hips',.003)
for x in [-.052,.052]:box('BuckleVertical',(x,-.185,1.09),(.017,.021,.092),14,'Hips',.003)
for z in [1.045,1.135]:box('BuckleHorizontal',(0,-.185,z),(.12,.021,.017),14,'Hips',.003)
box('BuckleTongue',(.023,-.20,1.09),(.061,.015,.013),14,'Hips',.003)
tube('NeckCap',[(0,0,1.765),(0,0,1.80)],[.097,.092],13,'Chest',n=12)
for j in range(6):
    a=j*math.tau/6
    ell('NeckBolt',(.068*math.cos(a),.068*math.sin(a),1.803),(.011,.011,.006),14,'Chest',8,4)

arm=bpy.data.armatures.new('KamikazeSkeleton')
rig=bpy.data.objects.new('KamikazeRig',arm);asset.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
boneinfo={}
def bone(name,head,tail,parent=None):
    b=arm.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=arm.edit_bones[parent]
    boneinfo[name]=(Vector(head),Vector(tail))
bone('Root',(0,0,0),(0,0,.15))
bone('Hips',(0,0,1.04),(0,0,1.18),'Root')
bone('Spine',(0,0,1.18),(0,0,1.43),'Hips')
bone('Chest',(0,0,1.43),(0,0,1.76),'Spine')
for s,side in [(-1,'R'),(1,'L')]:
    bone('Clavicle_'+side,(0,0,1.61),(s*.325,0,1.625),'Chest')
    bone('UpperArm_'+side,(s*.325,0,1.625),(s*.66,0,1.405),'Clavicle_'+side)
    bone('Forearm_'+side,(s*.66,0,1.405),(s*.86,0,1.775),'UpperArm_'+side)
    bone('Hand_'+side,(s*.86,0,1.775),(s*.91,0,1.88),'Forearm_'+side)
    bone('BombSocket_'+side,(s*.98,0,1.985),(s*.98,0,2.08),'Hand_'+side)
    bone('Thigh_'+side,(s*.145,0,1.04),(s*.22,0,.57),'Hips')
    bone('Calf_'+side,(s*.22,0,.57),(s*.235,0,.18),'Thigh_'+side)
    bone('Knee_'+side,(s*.22,0,.57),(s*.225,0,.49),'Thigh_'+side)
    bone('Foot_'+side,(s*.235,0,.18),(s*.235,-.21,.08),'Calf_'+side)
    bone('Toe_'+side,(s*.235,-.21,.08),(s*.235,-.28,.065),'Foot_'+side)
bpy.ops.object.mode_set(mode='OBJECT')

def distance(p,a,b):
    ab=b-a;t=max(0,min(1,(p-a).dot(ab)/ab.length_squared));return (p-a-ab*t).length
def auto_weights(obj,names):
    obj.vertex_groups.clear()
    groups={n:obj.vertex_groups.new(name=n) for n in names}
    for v in obj.data.vertices:
        p=obj.matrix_world@v.co
        ds=sorted([(distance(p,*boneinfo[n]),n) for n in names])[:3]
        values=[(math.exp(-d*d/.0085),n) for d,n in ds]
        total=sum(w for w,n in values)
        if total<1e-10:values=[(1,ds[0][1])];total=1
        for w,n in values:
            if w/total>.015:groups[n].add([v.index],w/total,'REPLACE')
auto_weights(body,['Hips','Spine','Chest']+[p+'_'+side for side in ['L','R'] for p in ['Clavicle','UpperArm','Forearm']])
pantsob.vertex_groups.clear()
pg={n:pantsob.vertex_groups.new(name=n) for n in ['Hips']+[p+'_'+side for side in ['L','R'] for p in ['Thigh','Knee','Calf']]}
for v in pantsob.data.vertices:
    p=pantsob.matrix_world@v.co;side='L' if p.x>0 else 'R'
    hipw=max(0,min(1,(p.z-.91)/.15))
    if abs(p.x)<.11:hipw=max(hipw,max(0,min(1,(p.z-.82)/.14)))
    kneew=math.exp(-((p.z-.57)/.072)**2)*.94
    upper=max(0,min(1,(p.z-.50)/.14))
    ws={'Hips':hipw,'Knee_'+side:(1-hipw)*kneew,'Thigh_'+side:(1-hipw)*(1-kneew)*upper,'Calf_'+side:(1-hipw)*(1-kneew)*(1-upper)}
    for n,w in ws.items():
        if w>.001:pg[n].add([v.index],w,'REPLACE')
character=join(parts[:],'KamikazeMesh')
character.parent=rig
mod=character.modifiers.new('KamikazeSkin','ARMATURE');mod.object=rig
bpy.context.view_layer.objects.active=character
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
oldmaterials=list(character.data.materials)
while character.data.uv_layers:character.data.uv_layers.remove(character.data.uv_layers[0])
uv=character.data.uv_layers.new(name='PaletteUV')
for p in character.data.polygons:
    mat=oldmaterials[p.material_index]
    index=mats.index(mat)
    for li in p.loop_indices:uv.data[li].uv=((index%4+.5)/4,(index//4+.5)/4)
    p.use_smooth=False
img=bpy.data.images.new('KamikazePalette',width=64,height=64,alpha=False)
pixels=[]
for y in range(64):
    for x in range(64):pixels.extend(palette[(y//16)*4+x//16])
img.pixels.foreach_set(pixels);img.filepath_raw=os.path.join(EXPORT,'KamikazePalette.png');img.file_format='PNG';img.save();img.pack()
material=bpy.data.materials.new('KamikazeAtlas');material.use_nodes=True
bsdf=material.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.65;bsdf.inputs['Specular IOR Level'].default_value=.15
tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest'
material.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
character.data.materials.clear();character.data.materials.append(material)
for p in character.data.polygons:p.material_index=0

def set_world(name,head,tail,roll_reference=None):
    pb=rig.pose.bones[name];direction=Vector(tail)-Vector(head)
    rest=arm.bones[name].matrix_local
    restdirection=boneinfo[name][1]-boneinfo[name][0]
    mat=(restdirection.rotation_difference(direction).to_matrix()@rest.to_3x3()).to_4x4()
    mat.translation=Vector(head);pb.matrix=mat

def ik(a,btarget,l1,l2,knee_dir):
    delta=btarget-a;dist=min(delta.length,l1+l2-.001);axis=delta.normalized()
    along=(l1*l1-l2*l2+dist*dist)/(2*dist)
    kd=Vector(knee_dir);kd=(kd-axis*kd.dot(axis)).normalized()
    return a+axis*along+kd*math.sqrt(max(0,l1*l1-along*along))

def key_pose(frame):
    for pb in rig.pose.bones:
        pb.rotation_mode='QUATERNION'
        pb.keyframe_insert('location',frame=frame,group=pb.name)
        pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
        pb.keyframe_insert('scale',frame=frame,group=pb.name)

rig.animation_data_create()
for actionname,frames in [('KamikazeIdle',61),('KamikazeRun',19)]:
    act=bpy.data.actions.new(actionname);rig.animation_data.action=act;act.use_fake_user=True
    for frame in range(1,frames+1):
        t=(frame-1)/(frames-1);scene.frame_set(frame)
        for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
        if actionname=='KamikazeIdle':
            rig.pose.bones['Spine'].rotation_quaternion=Vector((1,0,0)).rotation_difference(Vector((1,0,.008*math.sin(t*math.tau))))
            for s,side in [(-1,'R'),(1,'L')]:
                rig.pose.bones['UpperArm_'+side].rotation_quaternion=__import__('mathutils').Quaternion((0,1,0),s*.018*math.sin(t*math.tau))
        else:
            halfphase=t%.5
            lift=.09*math.sin(math.pi*(halfphase-.2)/.3) if halfphase>.2 else 0
            hips=rig.pose.bones['Hips'];hips.location=(0,-.12+lift,0)
            rig.pose.bones['Spine'].rotation_quaternion=__import__('mathutils').Quaternion((1,0,0),.15)
            rig.pose.bones['Chest'].rotation_quaternion=__import__('mathutils').Quaternion((0,0,1),.06*math.sin(t*math.tau))
            bpy.context.view_layer.update()
            for s,side in [(-1,'R'),(1,'L')]:
                phase=(t+(0 if side=='L' else .5))%1
                if phase<.2:
                    u=phase/.2;y=-.42+.84*u;z=.18
                else:
                    u=(phase-.2)/.8;y=.42-.84*(u*u*(3-2*u));z=.18+.33*math.sin(math.pi*u)
                hip=rig.pose.bones['Thigh_'+side].head.copy()
                ankle=Vector((s*.235,y,z))
                l1=(boneinfo['Thigh_'+side][1]-boneinfo['Thigh_'+side][0]).length
                l2=(boneinfo['Calf_'+side][1]-boneinfo['Calf_'+side][0]).length
                if (ankle-hip).length>l1+l2-.001:ankle=hip+(ankle-hip).normalized()*(l1+l2-.001)
                knee=ik(hip,ankle,l1,l2,(0,-1,.1))
                set_world('Thigh_'+side,hip,knee)
                bpy.context.view_layer.update()
                set_world('Calf_'+side,knee,ankle)
                bpy.context.view_layer.update()
                direction=((knee-hip).normalized()+(ankle-knee).normalized()).normalized()
                set_world('Knee_'+side,knee,knee+direction*.08)
                bpy.context.view_layer.update()
                footangle=0 if phase<.2 else .55*math.sin(math.pi*(phase-.2)/.8)
                toe=ankle+Vector((0,-.21*math.cos(footangle),-.10+.21*math.sin(footangle)))
                set_world('Foot_'+side,ankle,toe)
                rig.pose.bones['UpperArm_'+side].rotation_quaternion=__import__('mathutils').Quaternion((1,0,0),.07*math.sin(math.tau*t+s*.7))
                rig.pose.bones['Forearm_'+side].rotation_quaternion=__import__('mathutils').Quaternion((0,1,0),s*.045*math.sin(math.tau*t))
        bpy.context.view_layer.update();key_pose(frame)
    for layer in act.layers:
        for strip in layer.strips:
            for channelbag in strip.channelbags:
                for fc in channelbag.fcurves:
                    for kp in fc.keyframe_points:kp.interpolation='LINEAR'
rig.animation_data.action=bpy.data.actions['KamikazeIdle']
scene.frame_set(1)
scene.frame_start=1;scene.frame_end=19

def studiolink(o):
    for c in list(o.users_collection):c.objects.unlink(o)
    studio.objects.link(o)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.01))
floor=bpy.context.object;floor.name='StudioFloor';studiolink(floor)
fm=bpy.data.materials.new('StudioGray');fm.diffuse_color=(.22,.235,.25,1);floor.data.materials.append(fm)
for name,loc,power,size in [('Key',(-3,-4,5),650,4),('Fill',(4,-1,3),400,3),('Rim',(1,3,4),800,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((0,0,1.2))-ob.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('KamikazePreviewCamera');camera=bpy.data.objects.new('KamikazePreviewCamera',data);studio.objects.link(camera);scene.camera=camera
data.type='ORTHO';data.ortho_scale=2.85
def camera_view(loc,target=(0,0,1.15)):
    camera.location=loc;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
camera_view((3,-6,3.1))
scene.view_settings.view_transform='AgX'
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);character.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT,'Kamikaze.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='RELATIVE',use_mesh_modifiers=True)
bpy.data.libraries.write(os.path.join(SOURCE,'Kamikaze.blend'),{scene,bpy.data.actions['KamikazeIdle'],bpy.data.actions['KamikazeRun']},fake_user=True,compress=True)
me=character.data;me.calc_loop_triangles()
report={'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'bones':len(arm.bones),'materials':len(me.materials),'actions':[(a.name,tuple(a.frame_range)) for a in bpy.data.actions if a.name in ['KamikazeIdle','KamikazeRun']],'dimensions':list(character.dimensions),'front':'Blender -Y','source_scene':old_scene.name,'asset_scene':scene.name}
with open(os.path.join(SOURCE,'KamikazeBuildStats.json'),'w') as fp:json.dump(report,fp,indent=2)
result=report
