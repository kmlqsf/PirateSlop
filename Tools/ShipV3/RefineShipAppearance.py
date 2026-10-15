import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

base=Path(__file__).resolve().parents[2]
folder=base/'Assets/Models/Ships/ShipV3/Bilge'
manifest=json.loads((folder/'BilgeDeck.json').read_text())
document=json.loads((folder.parent/'ShipV3.json').read_text())
root=bpy.data.objects['Ship_V3_Fitted_Root']
assert root.get('appearance_repair_version') in (1,2)
matrices={o.name:o.matrix_world.copy() for o in bpy.data.objects}
changed=set(manifest['appearance_objects'])

def replace(obj,positions,faces,materials,uvs,face_materials):
    mesh=bpy.data.meshes.new(obj.name+'_AppearanceSurface')
    mesh.from_pydata([matrices[obj.name].inverted()@p for p in positions],[],faces)
    for material in materials:mesh.materials.append(material)
    uv=mesh.uv_layers.new(name='UVMap')
    for polygon,coords,mat in zip(mesh.polygons,uvs,face_materials):
        polygon.material_index=mat
        polygon.use_smooth=False
        for loop,coord in zip(polygon.loop_indices,coords):uv.data[loop].uv=coord
    mesh.update()
    mesh.validate(clean_customdata=True)
    mesh.update()
    obj.data=mesh
    changed.add(obj.name)

lining_script=base/'Tools/ShipV3/BuildShipInteriorLining.py'
lining_namespace=dict(globals(),__file__=str(lining_script))
exec(compile(lining_script.read_text(),str(lining_script),'exec'),lining_namespace)
manifest=json.loads((folder/'BilgeDeck.json').read_text())
changed=set(manifest['appearance_objects'])

if not root.get('forecastle_riser_seal_version'):
    for piece in document['pieces']:
        if not piece['intact'].startswith('V3_Forecastle_Riser_Plank_'):continue
        lo,hi=piece['bounds_min'][2],piece['bounds_max'][2]
        target_lo=8.48 if lo<8.8 else lo-.006
        target_hi=9.72 if hi>9.5 else hi+.006
        for name in [piece['intact']]+piece['fragment_names']:
            obj=bpy.data.objects[name];obj.data=obj.data.copy()
            inverse=matrices[name].inverted()
            for vertex in obj.data.vertices:
                p=matrices[name]@vertex.co
                p.z=target_lo+(p.z-lo)/(hi-lo)*(target_hi-target_lo)
                vertex.co=inverse@p
            obj.data.update();changed.add(name)
    root['forecastle_riser_seal_version']=1

for piece in document['pieces']:
    lo,hi=piece['bounds_min'],piece['bounds_max']
    if not piece['intact'].startswith('V3_Transfer_S000_Bollards_') and piece['intact'] not in ['V3_Transfer_S029_Helm_P0553_Intact','V3_Transfer_S053_MizzenMast_P0992_Intact']:continue
    if piece['family'] in ('Hull','Deck') or not -21<lo[1]<-10 or not 11.2<lo[2]<11.605:continue
    for name in [piece['intact']]+piece['fragment_names']:
        obj=bpy.data.objects[name]
        if obj.data.users>1:obj.data=obj.data.copy()
        inverse=matrices[name].inverted()
        for vertex in obj.data.vertices:
            p=matrices[name]@vertex.co
            p.z=max(p.z,11.605)
            vertex.co=inverse@p
        obj.data.update()
        changed.add(name)

attachment_script=base/'Tools/ShipV3/FitBoardingNetAttachments.py'
attachment_namespace=dict(globals(),__file__=str(attachment_script))
exec(compile(attachment_script.read_text(),str(attachment_script),'exec'),attachment_namespace)
manifest=json.loads((folder/'BilgeDeck.json').read_text())
changed=set(manifest['appearance_objects'])

manifest['appearance_objects']=sorted(changed)
manifest['modified_objects']=sorted(set(manifest['modified_objects'])|changed-set(manifest['new_objects']))
manifest['appearance_repair_version']=2
(folder/'BilgeDeck.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
root['appearance_repair_version']=2
bpy.context.view_layer.update()
joint_script=base/'Tools/ShipV3/FitBowHandrailJoints.py'
joint_namespace=dict(globals(),__file__=str(joint_script))
exec(compile(joint_script.read_text(),str(joint_script),'exec'),joint_namespace)
result={'version':2,'appearance_meshes':joint_namespace['result']['appearance_meshes']}
