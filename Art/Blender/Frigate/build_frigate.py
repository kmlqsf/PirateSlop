import bpy, math, json, os
from mathutils import Vector
from collections import defaultdict
ROOT=r'C:\Users\K\Project'
OUT=ROOT+'/Assets/Models/Ships/Frigate'
scene=bpy.data.scenes.get('PirateFrigate')
if scene:
 for obj in list(scene.objects): bpy.data.objects.remove(obj,do_unlink=True)
else: scene=bpy.data.scenes.new('PirateFrigate')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
palette={'Hull':(.065,.095,.092,1),'Teal':(.075,.22,.22,1),'Wood':(.25,.115,.045,1),'Deck':(.50,.30,.13,1),'DeckLight':(.60,.38,.18,1),'DeckDark':(.39,.22,.095,1),'Brass':(.65,.43,.15,1),'Iron':(.055,.065,.07,1),'Canvas':(.78,.70,.50,1),'CanvasShade':(.66,.58,.39,1),'Rope':(.30,.24,.14,1),'Glass':(.20,.43,.44,1),'Black':(.018,.027,.032,1),'Bone':(.84,.78,.60,1)}
mats={}
for name,color in palette.items():
 m=bpy.data.materials.get('Frigate_'+name) or bpy.data.materials.new('Frigate_'+name)
 m.diffuse_color=color; m.use_nodes=True
 bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value=color; bs.inputs['Roughness'].default_value=.7 if name not in ('Brass','Glass') else .32
 bs.inputs['Metallic'].default_value=.65 if name in ('Brass','Iron') else 0
 mats[name]=m
batches=defaultdict(lambda:[[],[],[]]); collisions=[]; sails=[]
def V(p): return (p[0],-p[2],p[1])
def mesh(group,verts,faces,mat):
 a,b,c=batches[group]; n=len(a); a.extend([V(v) for v in verts]); b.extend([tuple(n+i for i in f) for f in faces]); c.extend([mat]*len(faces))
def box(name,p,s,mat='Wood',r=0,solid=False):
 x,y,z=p; a,b,c=[v*.5 for v in s]; ang=math.radians(r); co=math.cos(ang); si=math.sin(ang)
 verts=[(x+dx*co+dz*si,y+dy,z-dx*si+dz*co) for dx,dy,dz in [(-a,-b,-c),(a,-b,-c),(a,-b,c),(-a,-b,c),(-a,b,-c),(a,b,-c),(a,b,c),(-a,b,c)]]
 mesh(name,verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)
 if solid: collisions.append({'name':name,'p':p,'s':s,'r':[0,r,0]})
def tube(name,a,b,r,mat='Rope',n=8,r2=None):
 a=Vector(a); b=Vector(b); d=(b-a).normalized(); u=d.cross(Vector((0,1,0)))
 if u.length<.01: u=d.cross(Vector((1,0,0)))
 u.normalize(); v=d.cross(u); r2=r if r2 is None else r2
 verts=[tuple(p+(math.cos(i*math.tau/n)*u+math.sin(i*math.tau/n)*v)*rad) for p,rad in ((a,r),(b,r2)) for i in range(n)]
 faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 mesh(name,verts,faces,mat)
def line(name,points,r,mat='Rope',n=6):
 for a,b in zip(points,points[1:]): tube(name,a,b,r,mat,n)
def ring(name,center,r,thick,mat='Brass',plane='xz',n=24):
 x,y,z=center; pts=[]
 for i in range(n+1):
  a=i*math.tau/n; pts.append((x+r*math.cos(a),y+(r*math.sin(a) if plane=='xy' else 0),z+(r*math.sin(a) if plane=='xz' else 0)))
 line(name,pts,thick,mat,6)
profile=[(-23,3.8),(-21,4.8),(-18,5.6),(-15,6.05),(-12,6.35),(-9,6.5),(-6,6.5),(-3,6.5),(0,6.4),(3,6.2),(6,5.85),(9,5.4),(12,4.8),(15,4.05),(18,3.05),(20,2.1),(22,1.0),(23,.08)]
def width(z):
 for (a,wa),(b,wb) in zip(profile,profile[1:]):
  if a<=z<=b: return wa+(wb-wa)*(z-a)/(b-a)
 return profile[0][1] if z< -23 else .08
def wedge(name,z0,z1,x0a,x0b,x1a,x1b,top,thick,mat,solid=True):
 verts=[(x0a,top-thick,z0),(x0b,top-thick,z0),(x1b,top-thick,z1),(x1a,top-thick,z1),(x0a,top,z0),(x0b,top,z0),(x1b,top,z1),(x1a,top,z1)]
 mesh(name,verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)
 if solid: collisions.append({'name':name,'vertices':verts})
levels=[(-2.9,.15),(-2.2,.46),(-1.3,.72),(-.3,.87),(.65,.94),(1.5,.985),(2.4,1.0),(3.25,1.005),(4.25,1.0)]
for j,((za,wa),(zb,wb)) in enumerate(zip(profile,profile[1:])):
 for k,((ya,fa),(yb,fb)) in enumerate(zip(levels,levels[1:])):
  for side in (-1,1):
   col='Hull' if k<4 else ('Teal' if k in (4,6) else 'Wood')
   verts=[(side*wa*fa,ya,za),(side*wb*fa,ya,zb),(side*wb*fb,yb,zb),(side*wa*fb,yb,za)]
   mesh('FrigateHull',verts,[(3,2,1,0)] if side>0 else [(0,1,2,3)],col)
   line('HullSeams',[(side*wa*fa,ya+.015,za),(side*wb*fa,ya+.015,zb)],.035,'Wood' if k<4 else 'Iron')
 for side in (-1,1):
  for y,f in [(.72,.945),(2.4,1.009),(4.25,1.013)]:
   tube('HullWales',(side*wa*f,y,za),(side*wb*f,y,zb),.09,'Brass' if y==2.4 else 'Hull',8)
  mx=side*(wa+wb)*.5; midz=(za+zb)*.5; length=math.hypot(wb-wa,zb-za)
  yaw=math.degrees(math.atan2(side*(wb-wa),zb-za))
  collisions.append({'name':'HullSide','p':[mx,2.65,midz],'s':[.35,3.3,length+.1],'r':[0,yaw,0]})
  for yy in (1.65,3.5):
   for zz in (za+.25,zb-.25): tube('HullFasteners',(side*(width(zz)+.04),yy,zz),(side*(width(zz)+.09),yy,zz),.045,'Iron',8)
 wedge('HoldFloor',za,zb,-wa*.98,wa*.98,-wb*.98,wb*.98,1,.3,'DeckDark')
zlist=sorted(set([p[0] for p in profile]+[2,10,-12]))
for za,zb in zip(zlist,zlist[1:]):
 wa=width(za); wb=width(zb)
 spans=[(-wa,wa,-wb,wb)] if not (za>=2 and zb<=10) else [(-wa,-1.4,-wb,-1.4),(1.4,wa,1.4,wb)]
 for aa,ab,ba,bb in spans: wedge('MainDeck',za,zb,aa,ab,ba,bb,4.3,.22,'Deck')
 if zb<=-12: wedge('Quarterdeck',za,zb,-wa+.15,wa-.15,-wb+.15,wb-.15,7.1,.24,'Deck')
for z in [i*.46-22.8 for i in range(99)]:
 w=width(z)-.1
 spans=[(-w,w)] if not 2<z<10 else [(-w,-1.43),(1.43,w)]
 for a,b in spans:
  if b>a: box('DeckCaulking',((a+b)*.5,4.308,z),(b-a,.008,.018),'Wood')
 if z < -12: box('DeckCaulking',(0,7.108,z),(w*2-.2,.008,.018),'Wood')
for x in range(-5,6):
 for z in range(-21,22,3):
  if abs(x)<width(z)-.2 and not (-1.5<x<1.5 and 2<z<10):
   box('DeckButts',(x,4.312,z+((x%2)*1.4)),(.018,.01,.44),'DeckDark')
for j,((ya,fa),(yb,fb)) in enumerate(zip(levels,levels[1:])):
 mesh('Transom',[(-3.8*fa,ya,-23),(3.8*fa,ya,-23),(3.8*fb,yb,-23),(-3.8*fb,yb,-23)],[(0,1,2,3)],'Hull' if j<4 else 'Teal')
for (za,wa),(zb,wb) in zip(profile,profile[1:]):
 mesh('Keel',[(-wa*.15,-2.9,za),(wa*.15,-2.9,za),(wb*.15,-2.9,zb),(-wb*.15,-2.9,zb)],[(0,1,2,3)],'Hull')
for (ya,fa),(yb,fb) in zip(levels,levels[1:]):
 mesh('StemClosure',[(-.08*fa,ya,23),(.08*fa,ya,23),(.08*fb,yb,23),(-.08*fb,yb,23)],[(0,1,2,3)],'Hull')
collisions.append({'name':'Transom','p':[0,2.65,-23],'s':[7.6,3.3,.2],'r':[0,0,0]})
box('CaptainCabinBack',(0,5.65,-22),(8.3,2.7,.32),'Teal',solid=True)
for side in (-1,1):
 box('CabinSide',(side*4,5.65,-17),( .24,2.7,10),'Teal',solid=True)
 box('CabinFront',(side*2.65,5.65,-12),(2.7,2.7,.24),'Wood',solid=True)
box('DoorLintel',(0,6.8,-12),(2.7,.35,.35),'Brass',solid=True)
for x in [-2.8,-1.4,0,1.4,2.8]:
 box('SternWindows',(x,5.7,-22.2),(1.04,1.45,.06),'Glass')
 for dx in [-.55,0,.55]: box('WindowFrames',(x+dx,5.7,-22.27),(.06,1.65,.08),'Brass')
 for y in [4.93,5.7,6.47]: box('WindowFrames',(x,y,-22.27),(1.15,.055,.08),'Brass')
for y in [4.55,6.85,7.35]: box('SternCornice',(0,y,-22.35),(8.55,.17,.4),'Brass')
for side in [-1,1]:
 for zz in [-14,-17,-20]:
  box('CabinWindow',(side*4.14,5.7,zz),(.08,1.45,1.25),'Glass')
  for off in [-.68,0,.68]: box('CabinFrames',(side*4.2,5.7,zz+off),(.1,1.6,.06),'Brass')
  for yy in [4.95,6.45]: box('CabinFrames',(side*4.2,yy,zz),(.1,.07,1.5),'Brass')
def stairs(name,x,z0,z1,y0,y1,w,steps):
 for i in range(steps):
  t=(i+.5)/steps; z=z0+(z1-z0)*t; y=y0+(y1-y0)*(i+1)/steps
  box(name,(x,y-.09,z),(w,.18,abs(z1-z0)/steps+.025),'DeckLight')
  rise=abs(y1-y0)/steps
  box(name+'Riser',(x,y-rise*.5-.075,z-(z1-z0)/steps*.48),(w,rise+.15,.07),'Wood')
  box('StairNosing',(x,y+.015,z-(z1-z0)/steps*.43),(w,.04,.045),'Brass')
 a=(x,y0-.12,z0); b=(x,y1-.12,z1)
 for side in [-1,1]:
  tube('StairStringers',(x+side*(w*.5-.07),y0-.1,z0),(x+side*(w*.5-.07),y1-.1,z1),.11,'Wood',6)
  tube('StairRails',(x+side*(w*.5+.08),y0+1,z0),(x+side*(w*.5+.08),y1+1,z1),.075,'Brass',8)
  for t in [0,.33,.67,1]:
   z=z0+(z1-z0)*t; y=y0+(y1-y0)*t
   tube('StairPosts',(x+side*(w*.5+.08),y,z),(x+side*(w*.5+.08),y+1,z),.07,'Wood')
 length=math.hypot(z1-z0,y1-y0); angle=-math.degrees(math.atan2(y1-y0,z1-z0))
 collisions.append({'name':name+'Ramp','p':[x,(y0+y1)*.5-.09,(z0+z1)*.5],'s':[w,.18,length+.2],'r':[angle,0,0]})
stairs('HoldStairs',0,3,10,1,4.3,2.3,15)
for x in [-4.65,4.65]: stairs('BridgeStairs',x,-6,-12,4.3,7.1,1.85,13)
for x in [-1.58,1.58]:
 tube('HatchRails',(x,5.3,2),(x,5.3,9.5),.07,'Brass')
 for z in [2,4.5,7,9.5]: box('HatchPosts',(x,4.8,z),(.11,1,.11),'Wood',solid=True)
 collisions.append({'name':'HatchRail','p':[x,5.22,5.75],'s':[.12,.18,7.5],'r':[0,0,0]})
box('HatchEndRail',(0,5.2,2),(3.2,.16,.14),'Brass',solid=True)
for side in [-1,1]:
 for (za,wa),(zb,wb) in zip(profile,profile[1:]):
  y=7.1 if zb<=-12 else 4.3
  a=(side*wa,y+.22,za); b=(side*wb,y+.22,zb)
  tube('BulwarkSill',a,b,.2,'Teal',8)
  length=math.hypot(wb-wa,zb-za); yaw=math.degrees(math.atan2(side*(wb-wa),zb-za))
  collisions.append({'name':'GunBaySill','p':[side*(wa+wb)*.5,y+.2,(za+zb)*.5],'s':[.22,.4,length],'r':[0,yaw,0]})
  if (za>=-10 and zb<=18) or za>=22:
   continue
  tube('RailCap',(side*wa,y+1.04,za),(side*wb,y+1.04,zb),.09,'Brass')
  for z in [za,(za+zb)*.5]: box('RailPosts',(side*width(z),y+.65,z),(.13,.9,.13),'Wood')
 for z in [-10,-6,-2,2,6,10,14,18]:
  x=side*width(z)
  box('GunBayCheek',(x,4.93,z),(.25,1.25,.7),'Teal',solid=True)
  box('GunBayCap',(x,5.58,z),(.32,.12,.84),'Brass')
  for y in [4.55,5.3]: tube('GunBayStuds',(x-side*.16,y,z-.22),(x-side*.2,y,z-.22),.045,'Brass')
 # quarterdeck forward edge leaves two stair openings
 box('BridgeFrontRail',(side*1.65,8.12,-12),(3.2,.16,.18),'Brass',solid=True)
 for x in [side*.2,side*1.6,side*3.2]: box('BridgePosts',(x,7.62,-12),(.14,1,.14),'Wood')
box('SternRail',(0,8.15,-22.7),(7.4,.16,.18),'Brass',solid=True)
for x in [-3.4,-1.7,0,1.7,3.4]: box('SternPosts',(x,7.6,-22.7),(.14,1,.14),'Wood')
for name,z,top,base in [('Fore',13,28,1),('Main',-2,33,1),('Mizzen',-16,25,4.3)]:
 tube(name+'Mast',(0,base,z),(0,top,z),.34 if name=='Main' else .28,'Wood',12,r2=.12)
 for y in [4.5,5,9,13,17,21,25]:
  if y<top: ring('MastBands',(0,y,z),.33 if name=='Main' else .27,.045,'Iron')
 collisions.append({'name':name+'Mast','p':[0,(base+top)*.5,z],'s':[.62,top-base,.62],'r':[0,0,0]})
 for level,(yy,w,h) in enumerate([(top-3,6 if name=='Mizzen' else 7.2,5),(top-10,6.5 if name=='Mizzen' else 9,6)]):
  tube('Yards',(-w-.6,yy,z+.35),(w+.6,yy,z+.35),.12,'Wood',10,r2=.08)
  group='Sail_'+name+str(level); nx=16; ny=10
  verts=[]
  for row in range(ny+1):
   v=row/ny
   for col in range(nx+1):
    u=col/nx; x=(u*2-1)*w*(1-.09*v)
    y=yy-h*v+.4*math.sin(u*math.pi)*v*v
    zz=z+.38+1.55*math.sin(u*math.pi)*math.sin(v*math.pi*.9)
    verts.append((x,y,zz))
  faces=[]
  for row in range(ny):
   for col in range(nx):
    a=row*(nx+1)+col; faces.append((a,a+1,a+nx+2,a+nx+1))
  mesh(group,verts,faces,'Canvas')
  for col in range(0,nx+1,2):
   pts=[tuple(Vector(verts[row*(nx+1)+col])+Vector((0,0,.02))) for row in range(ny+1)]
   line(group,pts,.014,'CanvasShade',5)
  for row in [0,ny]: line(group,[verts[row*(nx+1)+col] for col in range(nx+1)],.04,'Rope')
  for col in [0,nx]: line(group,[verts[row*(nx+1)+col] for row in range(ny+1)],.04,'Rope')
  sails.append({'name':group,'pivot':[0,yy,z+.35]})
  for side in [-1,1]:
   tube('RunningRigging',(side*(w+.45),yy,z+.35),(side*min(width(z)-.5,5.2),4.8 if z>-12 else 7.6,z-2),.027,'Rope',6)
 for side in [-1,1]:
  x=side*(width(z)-.45); deck=4.3 if z>-12 else 7.1
  for dz in [-1.7,0,1.7]:
   tube('StandingRigging',(x,deck+.25,z+dz),(side*.18,top-10,z),.045,'Rope',6)
   ring('Deadeyes',(x,deck+.6,z+dz),.15,.06,'Wood','xy',12)
  for i in range(1,27):
   t=i/28; yy=deck+.3+(top-10-deck-.3)*t; xx=x*(1-t)+side*.18*t
   tube('Ratlines',(xx,yy,z-1.7*(1-t)),(xx,yy,z+1.7*(1-t)),.018,'Rope',5)
 tube('MastTruck',(0,top,z),(0,top+.55,z),.11,'Brass',10)
for a,b in [((0,32,-2),(0,27,13)),((0,24,-16),(0,32,-2)),((0,27,13),(0,7,31)),((0,20,13),(0,6,29)),((0,32,-2),(0,4.5,22))]: tube('Forestays',a,b,.045,'Rope')
tube('Bowsprit',(0,3.8,19),(0,7,31),.3,'Wood',12,r2=.09)
walk_a=Vector((0,4.3,20)); walk_b=Vector((0,7.14,31)); delta=walk_b-walk_a
side=Vector((.42,0,0)); down=Vector((0,-.16,0))
verts=[tuple(p+offset) for p in (walk_a+down,walk_b+down,walk_a,walk_b) for offset in (-side,side)]
mesh('BowspritWalkway',verts,[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],'Wood')
collisions.append({'name':'BowspritWalkway','p':list((walk_a+walk_b)*.5+down*.5),'s':[.84,.16,delta.length+.05],'r':[-math.degrees(math.atan2(delta.y,delta.z)),0,0]})
for i in range(1,22):
 p=walk_a.lerp(walk_b,i/22)
 tube('BowspritTreads',tuple(p-side),tuple(p+side),.025,'DeckLight')
for z in [23,24,25,26,27,28]: ring('BowspritBindings',(0,3.8+(z-19)*3.2/12,z),.22,.035,'Rope','xy')
mesh('Jib',[(0,26,13),(0,7.2,30),(0,10,16)],[(0,1,2)],'Canvas')
mesh('JibInner',[(0,21,12),(0,7.3,25),(0,10.3,13)],[(0,1,2)],'CanvasShade')
nest_y=33.2; nest_z=-2
for i in range(20):
 a=i*math.tau/20; b=(i+1)*math.tau/20
 tube('NestBraces',(0,nest_y-1.5,nest_z),(1.65*math.cos(a),nest_y-.1,nest_z+1.65*math.sin(a)),.075,'Wood')
 if math.sin((a+b)*.5)>-.72:
  p=(1.68*math.cos(a),nest_y,nest_z+1.68*math.sin(a)); q=(p[0],nest_y+1,p[2])
  tube('NestPosts',p,q,.06,'Wood')
  tube('NestRails',q,(1.68*math.cos(b),nest_y+1,nest_z+1.68*math.sin(b)),.055,'Brass')
  mid=(a+b)*.5; collisions.append({'name':'NestRail','p':[1.68*math.cos(mid),nest_y+.85,nest_z+1.68*math.sin(mid)],'s':[.54,.3,.12],'r':[0,-math.degrees(mid),0]})
for i in range(20):
 a=i*math.tau/20; b=(i+1)*math.tau/20
 top=[(.27*math.cos(a),nest_y,nest_z+.27*math.sin(a)),(1.7*math.cos(a),nest_y,nest_z+1.7*math.sin(a)),(1.7*math.cos(b),nest_y,nest_z+1.7*math.sin(b)),(.27*math.cos(b),nest_y,nest_z+.27*math.sin(b))]
 verts=[(x,y-.2,z) for x,y,z in top]+top
 mesh('NestFloor',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],'Wood')
 collisions.append({'name':'NestFloor','vertices':verts})
for x in [-.48,.48]: tube('MastLadder',(x,4.3,-3.85),(x,nest_y+1,-3.85),.065,'Wood')
for i in range(int((nest_y-4.3)/.32)+1): tube('MastLadder',(-.48,4.4+i*.32,-3.85),(.48,4.4+i*.32,-3.85),.042,'Wood')
for y in [5.5+i*2.5 for i in range(12)]:
 for x in [-.4,.4]: tube('LadderBrackets',(x,y,-3.85),(x*.4,y,-2),.065,'Iron')
def barrel(x,y,z):
 n=12; verts=[]
 for yy,r in [(0,.34),(.15,.43),(.65,.48),(1.15,.43),(1.3,.34)]:
  for i in range(n): a=i*math.tau/n; verts.append((x+r*math.cos(a),y+yy,z+r*math.sin(a)))
 faces=[]
 for j in range(4):
  for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 faces.extend([tuple(range(n-1,-1,-1)),tuple(range(4*n,5*n))]); mesh('Barrels',verts,faces,'Wood')
 for yy,rr in [(.16,.44),(.4,.48),(.94,.48),(1.14,.44)]: ring('BarrelHoops',(x,y+yy,z),rr,.035,'Iron')
 collisions.append({'name':'Barrel','p':[x,y+.65,z],'s':[.85,1.3,.85],'r':[0,0,0]})
for side in [-1,1]:
 for zz in [-10,-8,-6]: barrel(side*3.6,1,zz)
 for zz in [-19,-17]: box('CargoCrates',(side*2.4,1.5,zz),(1.35,1,1.35),'Wood',solid=True)
 for zz in [-9,-5,-1,3,7,11]:
  x=side*(width(zz)-.8)
  tube('Cleats',(x,4.3,zz),(x,4.65,zz),.09,'Iron')
  tube('Cleats',(x,4.62,zz-.3),(x,4.62,zz+.3),.065,'Iron')
 for zz in [-20,-14,16]:
  x=side*(width(zz)-.3); y=8 if zz< -12 else 5.2
  box('Lanterns',(x,y,zz),(.3,.5,.3),'Brass')
  box('LanternGlass',(x,y+.02,zz),(.32,.31,.32),'Glass')
for z in [-19,-15,-11,-7,-3,1,11,15,19]:
 w=width(z)*.88
 for side in [-1,1]: tube('HoldRibs',(side*w*.9,1,z),(side*w,4.06,z),.1,'Wood')
 if not 2<z<10: tube('DeckBeams',(-w,3.98,z),(w,3.98,z),.13,'Wood')
for yy,rr in [(4.4,.7),(4.7,.42),(5.05,.5)]:
 tube('Capstan',(0,yy,17.5),(0,yy+.18,17.5),rr,'Wood',12)
for angle in [0,60,120]:
 a=math.radians(angle); tube('CapstanBars',(-1.1*math.cos(a),5.12,17.5-1.1*math.sin(a)),(1.1*math.cos(a),5.12,17.5+1.1*math.sin(a)),.065,'Wood')
for side in [-1,1]:
 x=side*3.6; z=16
 tube('Anchor',(x,3.5,z),(x,1,z),.1,'Iron'); tube('Anchor',(x,2.95,z-.55),(x,2.95,z+.55),.08,'Wood')
 line('Anchor',[(x,1.65,z-.85),(x,1.05,z-.6),(x,.9,z),(x,1.05,z+.6),(x,1.65,z+.85)],.1,'Iron')
 line('AnchorCable',[(x,3.5,z),(side*3.5,4.8,16),(side*2.7,4.5,15)],.06,'Rope')
box('HelmPedestal',(0,7.65,-17),( .45,1.1,.45),'Wood',solid=True)
ring('HelmWheel',(0,8.45,-16.8),.67,.065,'Wood','xy',32)
ring('HelmWheel',(0,8.45,-16.8),.53,.02,'Brass','xy',28)
for i in range(8):
 a=i*math.tau/8
 tube('HelmWheel',(0,8.45,-16.8),(.82*math.cos(a),8.45+.82*math.sin(a),-16.8),.045,'Wood')
 tube('HelmWheel',(.65*math.cos(a),8.45+.65*math.sin(a),-16.8),(.83*math.cos(a),8.45+.83*math.sin(a),-16.8),.068,'Brass')
tube('HelmWheel',(0,8.45,-16.7),(0,8.45,-16.9),.13,'Brass',12)
for side in [-1,1]:
 for base in [4.5,7.25]:
  pts=[]
  for i in range(30):
   t=i/29; a=t*math.tau*1.25; rr=.65*(1-t)+.08
   pts.append((side*(3.8+rr*math.cos(a)),base+rr*math.sin(a),-22.45))
  line('SternScrolls',pts,.055,'Brass')
verts=[(0,33.2,-2),(0,32.25,-2),(2.1,32.35,-2.05),(1.55,32.75,-1.98),(2.4,33.35,-2.1)]
mesh('PiratePennant',verts,[(0,1,2,3,4)],'Black')
for a,b in [((.6,32.55,-2.12),(1.25,33.15,-2.12)),((.6,33.15,-2.12),(1.25,32.55,-2.12))]: tube('PennantBones',a,b,.035,'Bone')
ring('PennantSkull',(.93,33.02,-2.13),.16,.06,'Bone','xy',12)
box('PennantJaw',(.93,32.87,-2.13),(.19,.12,.03),'Bone')
for group in ['PiratePennant','PennantBones','PennantSkull','PennantJaw']:
 verts,faces,colors=batches[group]
 batches[group][0]=[(x,y,z+2.4) for x,y,z in verts]
tube('FlagStaff',(0,33,-2),(0,36,-2),.09,'Wood')
for name,p in [('Origin',(0,0,0)),('Forward',(0,0,1)),('Up',(0,1,0)),('Right',(1,0,0))]:
 obj=bpy.data.objects.new('Anchor_'+name,None); scene.collection.objects.link(obj); obj.location=V(p)
import bmesh
for name,(verts,faces,colors) in batches.items():
 data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
 obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj)
 keys=list(dict.fromkeys(colors))
 for key in keys: data.materials.append(mats[key])
 for poly,col in zip(data.polygons,colors): poly.material_index=keys.index(col)
 if name not in ['FrigateHull','Jib','JibInner','PiratePennant','CrowsNest'] and not name.startswith('Sail_'):
  bm=bmesh.new(); bm.from_mesh(data); bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bm.to_mesh(data); bm.free()
 if name in ['CaptainCabinBack','CabinSide','HelmPedestal','GunBayCheek','CargoCrates']:
  mod=obj.modifiers.new('CraftedEdges','BEVEL'); mod.width=.025; mod.segments=2
os.makedirs(OUT,exist_ok=True)
scene.world=bpy.data.worlds.new('FrigateStudio'); scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.07,.095,.12,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
bpy.ops.object.select_all(action='DESELECT')
for obj in scene.objects: obj.select_set(True)
bpy.context.view_layer.objects.active=scene.objects.get('FrigateHull')
bpy.ops.export_scene.fbx(filepath=OUT+'/PirateFrigate.fbx',use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False)
manifest={'boxes':[c for c in collisions if 'p' in c],'hulls':[{'name':c['name'],'points':[v for p in c['vertices'] for v in p]} for c in collisions if 'vertices' in c],'sails':sails,'materials':[{'name':'Frigate_'+k,'color':list(v)} for k,v in palette.items()],'length':46,'beam':13,'deck':4.3,'bridge':7.1}
with open(OUT+'/FrigateLayout.json','w') as f: json.dump(manifest,f)
bpy.data.libraries.write(ROOT+'/Art/Blender/Frigate/PirateFrigate.blend',{scene},fake_user=True)
result={'objects':len(scene.objects),'polygons':sum(len(o.data.polygons) for o in scene.objects if o.type=='MESH'),'fbx':OUT+'/PirateFrigate.fbx','blend':ROOT+'/Art/Blender/Frigate/PirateFrigate.blend'}
