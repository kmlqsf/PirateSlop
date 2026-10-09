from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(root / "Library/StormRainAudioTools"))
import numpy as np
import soundfile as sf

folder = root / "Assets/Audio/Ambience/Storm"
for letter in "ABC":
    source = folder / f"Thunder{letter}.mp3"
    data, rate = sf.read(source, dtype="float32", always_2d=True)
    mono = np.mean(data, axis=1)
    sf.write(folder / f"TestThunder{letter}.wav", mono, rate, subtype="PCM_16")
    print(f"TestThunder{letter}: {len(mono) / rate:.2f}s, mono, {rate}Hz")
