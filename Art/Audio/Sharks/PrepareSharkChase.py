import array
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import wave

root = Path(__file__).resolve().parents[3] / 'Assets/Audio/Sharks'
original = root / 'Originals/fronbondi_skegs-sfx-custom-version-of-jaws-theme-cinematic-sound-effect-461780.mp3'
provided = Path('C:/Users/K/Downloads') / original.name
if not original.exists():
    shutil.copy2(provided, original)
with tempfile.TemporaryDirectory() as scratch:
    pitched = Path(scratch) / 'pitched.wav'
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(original), '-af',
                    'atrim=start=18.04:end=26.22,asetpts=PTS-STARTPTS,rubberband=pitch=1.2599210498948732:tempo=1',
                    '-ar', '44100', '-ac', '1', '-c:a', 'pcm_s16le', str(pitched)], check=True)
    with wave.open(str(pitched), 'rb') as f:
        rate = f.getframerate()
        pcm = array.array('h', f.readframes(f.getnframes()))
    overlap = round(.18 * rate)
    loop = pcm[overlap:]
    for i in range(overlap):
        u = i / (overlap - 1)
        loop[len(loop)-overlap+i] = round(pcm[len(pcm)-overlap+i] * (1-u) + pcm[i] * u)
    gain = .72 * 32767 / max(abs(v) for v in loop)
    loop = array.array('h', (round(v * gain) for v in loop))
    output = root / 'SharkChase.wav'
    with wave.open(str(output), 'wb') as f:
        f.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        f.writeframes(loop.tobytes())
    step = abs(loop[-1] - loop[0]) / 32768
    entries = json.loads((root / 'SOURCES.json').read_text(encoding='utf-8'))
    entries = [e for e in entries if e['file'] != 'SharkChase.wav']
    entries.append(dict(file='SharkChase.wav', source='Originals/' + original.name,
                        license='User-provided file; license not inferred from filename',
                        original_sha256=hashlib.sha256(original.read_bytes()).hexdigest(),
                        sha256=hashlib.sha256(output.read_bytes()).hexdigest(),
                        recipe=dict(trim_start=18.04, trim_end=26.22, pitch_semitones=4, tempo=1,
                                    processing='FFmpeg rubberband', mono=True, crossfade_seconds=.18, peak=.72),
                        duration=len(loop)/rate, seam_sample_step=step))
    (root / 'SOURCES.json').write_text(json.dumps(entries, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(duration=len(loop)/rate, pitch_semitones=4, seam_sample_step=step)))
