from pathlib import Path
import json
import shutil
import socket
import zipfile
from PIL import Image, ImageOps

root = Path(__file__).resolve().parents[2]
incoming = root.parent / 'Blender' / 'Лутабельные' / 'New'
source = root / 'Art' / 'Blender' / 'LootReplacement' / 'BoardingEquipment'
models = [('BoardingHarpoon', 'абордажный+снаряд.zip'), ('WineBottle', 'Вино.zip'), ('SpyglassTube', 'Подзорная+труба.zip')]
report = []
for key, archive in models:
    folder = source / key
    folder.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(incoming / archive) as z:
        for entry in z.infolist():
            dest = (folder / entry.filename).resolve()
            if not dest.is_relative_to(folder.resolve()):
                raise ValueError('Archive path escapes source directory')
            if entry.is_dir():
                dest.mkdir(parents=True, exist_ok=True)
            else:
                dest.parent.mkdir(parents=True, exist_ok=True)
                dest.write_bytes(z.read(entry))
    images = list(folder.rglob('*'))
    out = root / 'Assets' / 'Models' / 'Loot' / 'Replacement' / key
    out.mkdir(parents=True, exist_ok=True)
    for suffix, target in [('basecolor', 'BaseColor.jpg'), ('normal', 'Normal.png')]:
        image = Image.open(next(p for p in images if p.stem.endswith('_' + suffix)))
        image.convert('RGB').save(out / (key + target), quality=95)
    metallic = Image.open(next(p for p in images if p.stem.endswith('_metallic'))).convert('L')
    roughness = Image.open(next(p for p in images if p.stem.endswith('_roughness'))).convert('L').resize(metallic.size)
    Image.merge('RGBA', (metallic, metallic, metallic, ImageOps.invert(roughness))).save(out / (key + 'MetalSmooth.png'))
    report.append({'key': key, 'source': str(next(folder.rglob('*.fbx'))), 'output': str(out / (key + '.fbx'))})
(source / 'ImportManifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
code = 'project_root = ' + repr(str(root)) + '\n' + (root / 'Tools' / 'Art' / 'import_boarding_equipment.py').read_text(encoding='utf-8')
with socket.create_connection(('127.0.0.1', 9876), 10) as bridge:
    bridge.settimeout(120)
    bridge.sendall((json.dumps({'type': 'execute', 'code': code, 'strict_json': True}) + '\0').encode())
    data = b''
    while b'\0' not in data:
        chunk = bridge.recv(65536)
        if not chunk:
            raise RuntimeError('Blender closed the connection')
        data += chunk
    print(data.split(b'\0')[0].decode())
