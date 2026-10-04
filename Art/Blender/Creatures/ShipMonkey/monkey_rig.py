import bpy, math, os, json
from mathutils import Vector, Matrix

WORK=r'C:\Users\K\Documents\Codex\2026-10-04\new-chat-3\work'
mesh=bpy.data.objects['ShipMonkeyMesh']
if 'ShipMonkeyRig' in bpy.data.objects:
    old=bpy.data.objects['ShipMonkeyRig']
    mesh.parent=None
    for modifier in list(mesh.modifiers):
        if modifier.type=='ARMATURE': mesh.modifiers.remove(modifier)
    bpy.data.objects.remove(old,do_unlink=True)
    mesh.vertex_groups.clear()
data=bpy.data.armatures.new('ShipMonkeySkeleton')
rig=bpy.data.objects.new('ShipMonkeyRig',data)
bpy.context.scene.collection.objects.link(rig)
rig.show_in_front=True
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')

def bone(name,head,tail,parent=None,deform=True):
    obj=data.edit_bones.new(name);obj.head=head;obj.tail=tail
    if parent: obj.parent=data.edit_bones[parent]
    obj.use_deform=deform
    obj.align_roll(Vector((0,-1,0)))
    return obj

bone('Root',(0,0,0),(0,0,.1),deform=False)
bone('Pelvis',(0,.015,.292),(0,.012,.36),'Root')
bone('Spine',(0,.012,.36),(0,.01,.425),'Pelvis')
bone('Chest',(0,.01,.425),(0,.015,.51),'Spine')
bone('Neck',(0,.015,.51),(0,.01,.565),'Chest')
bone('Head',(0,.01,.565),(0,.015,.755),'Neck')
for side,sign in [('L',1),('R',-1)]:
    bone('Clavicle.'+side,(0,.01,.49),(sign*.105,.005,.485),'Chest')
    bone('UpperArm.'+side,(sign*.105,.005,.485),(sign*.235,.0,.485),'Clavicle.'+side)
    bone('Forearm.'+side,(sign*.235,.0,.485),(sign*.367,-.008,.483),'UpperArm.'+side)
    bone('Hand.'+side,(sign*.367,-.008,.483),(sign*.425,-.018,.48),'Forearm.'+side)
    bone('Fingers.'+side,(sign*.425,-.018,.48),(sign*.48,-.020,.478),'Hand.'+side)
    bone('Thumb.'+side,(sign*.385,-.035,.477),(sign*.413,-.059,.453),'Hand.'+side)
    bone('Thigh.'+side,(sign*.068,.018,.296),(sign*.088,.005,.168),'Pelvis')
    bone('Shin.'+side,(sign*.088,.005,.168),(sign*.102,.001,.056),'Thigh.'+side)
    bone('Foot.'+side,(sign*.102,.001,.056),(sign*.113,-.085,.025),'Shin.'+side)
    bone('Toes.'+side,(sign*.113,-.085,.025),(sign*.116,-.12,.024),'Foot.'+side)

tail=[(.045,-.044,.275),(.091,.004,.214),(.141,.049,.177),(.193,.098,.155),(.247,.148,.163),(.296,.199,.198),(.323,.226,.262),(.295,.187,.324),(.252,.141,.347),(.22,.113,.316),(.239,.131,.289)]
for index in range(len(tail)-1):
    bone('Tail%02d'%index,tail[index],tail[index+1],'Pelvis' if index==0 else 'Tail%02d'%(index-1))
bone('Sash',( .075,-.054,.305),(.115,-.065,.192),'Pelvis')
bone('Bandana',( .06,.076,.67),(.09,.12,.59),'Head')
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
bpy.context.view_layer.update()

deform_names=[b.name for b in rig.data.bones if b.use_deform]
groups={name:mesh.vertex_groups.get(name) or mesh.vertex_groups.new(name=name) for name in deform_names}
deform_ids={g.index for g in groups.values()}

def assign(index,weights):
    for g in list(mesh.data.vertices[index].groups):
        if g.group in deform_ids: mesh.vertex_groups[g.group].remove([index])
    total=sum(weights.values())
    for name,weight in weights.items():
        if weight>0: groups[name].add([index],weight/total,'REPLACE')

def segment_distance(p,a,b):
    v=b-a;t=max(0,min(1,(p-a).dot(v)/v.length_squared))
    return (p-a-v*t).length,t

tail_vectors=[Vector(t) for t in tail]
for vertex in mesh.data.vertices:
    p=vertex.co;x,y,z=p
    if z>.55:
        assign(vertex.index,{'Head':1})
    elif z>.52 and abs(x)<.13:
        t=max(0,min(1,(z-.52)/.03));assign(vertex.index,{'Neck':1-t,'Head':t})
    if y>.07 and z<.39 and (x>.15 or y>.14):
        ds=[(segment_distance(p,tail_vectors[i],tail_vectors[i+1])[0],i) for i in range(len(tail)-1)]
        closest=sorted(ds)[:2]
        if closest[0][0]<.06:
            weights={'Tail%02d'%i:math.exp(-d*d/(.028*.028)) for d,i in closest}
            assign(vertex.index,weights)
    if .0<x<.155 and y<-.055 and .165<z<.297:
        if x>.052 and z<.25:
            assign(vertex.index,{'Sash':1})

for vertex in mesh.data.vertices:
    entries=[(g.weight,g.group) for g in vertex.groups if g.group in deform_ids and g.weight>0]
    if len(entries)>4:
        best=sorted(entries,reverse=True)[:4]
        assign(vertex.index,{mesh.vertex_groups[index].name:weight for weight,index in best})
    elif not entries:
        names=[n for n in deform_names if not n.startswith(('Tail','Sash','Bandana'))]
        nearest=min(names,key=lambda n:segment_distance(vertex.co,rig.data.bones[n].head_local,rig.data.bones[n].tail_local)[0])
        assign(vertex.index,{nearest:1})

rig['WalkStride']=.40
rig['WalkCycleSeconds']=1.0
rig['ClimbStride']=.32
rig['ClimbCycleSeconds']=1.2
rig['ModelForward']='-Y'
bpy.context.scene.render.fps=30
result={'bones':len(rig.data.bones),'unweighted':sum(1 for v in mesh.data.vertices if not v.groups),'weights':{name:sum(1 for v in mesh.data.vertices if any(g.group==groups[name].index and g.weight>.2 for g in v.groups)) for name in deform_names}}
