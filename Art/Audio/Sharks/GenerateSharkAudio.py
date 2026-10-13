import array
import hashlib
import json
import math
from pathlib import Path
import urllib.request
import wave

root = Path(__file__).resolve().parents[3] / 'Assets/Audio/Sharks'
sources = []

def write(name, samples, rate=44100):
    peak = max(abs(v) for v in samples) or 1
    gain = .72 / peak
    pcm = array.array('h', (int(max(-1, min(1, v * gain)) * 32767) for v in samples))
    path = root / (name + '.wav')
    with wave.open(str(path), 'wb') as f:
        f.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        f.writeframes(pcm.tobytes())
    return hashlib.sha256(path.read_bytes()).hexdigest()

for name, slug, start, seconds, speed in [
    ('SharkSplash', 'water-splash-42', 0, 1.8, .88),
    ('SharkSwim', 'growth-d2bb5053136ce5a69e0403c1-part-1-v1', .15, 1.2, .9),
    ('SharkBite', 'cartoon-chomp-01', 0, 1, .82),
]:
    url = 'https://sfxmint.com/dl/' + slug + '.wav'
    original = root / 'Originals' / (slug + '.wav')
    if not original.exists():
        original.write_bytes(urllib.request.urlopen(url).read())
    with wave.open(str(original), 'rb') as f:
        channels, width, rate = f.getnchannels(), f.getsampwidth(), f.getframerate()
        if width != 2:
            raise ValueError('Expected PCM16')
        pcm = array.array('h', f.readframes(f.getnframes()))
    mono = [sum(pcm[i:i+channels]) / (32768 * channels) for i in range(0, len(pcm), channels)]
    mono = mono[int(start * rate):int((start + seconds) * rate)]
    samples = []
    for i in range(int(len(mono) / speed)):
        ix = min(len(mono)-1, int(i * speed))
        fade = min(1, i / (rate * .008), (len(mono)/speed-i) / (rate * .045))
        samples.append(mono[ix] * fade)
    sources.append(dict(file=name+'.wav', url=url, license='CC0-1.0', original_sha256=hashlib.sha256(original.read_bytes()).hexdigest(), sha256=write(name, samples, rate), recipe=dict(mono=True, start=start, seconds=seconds, speed=speed, peak=.72, fade_in=.008, fade_out=.045)))

(root / 'SOURCES.json').write_text(json.dumps(sources, ensure_ascii=False, indent=2), encoding='utf-8')
import subprocess
import sys
subprocess.run([sys.executable, str(Path(__file__).with_name('PrepareSharkChase.py'))], check=True)
