from pathlib import Path
import wave
import numpy as np

folder = Path(__file__).resolve().parents[3] / 'Assets' / 'Audio' / 'BottleBreak'
folder.mkdir(parents=True, exist_ok=True)
rate = 44100
for variant in range(1, 4):
    rng = np.random.default_rng(480 + variant)
    length = .70 + variant * .025
    t = np.arange(int(rate * length)) / rate
    audio = np.zeros_like(t)
    crack = rng.normal(0, 1, len(t))
    crack -= np.concatenate(([0], crack[:-1])) * .85
    audio += crack * np.exp(-t / .006) * .23
    audio += .09 * np.sin(2 * np.pi * 180 * t) * np.exp(-t / .014)
    for shard in range(27):
        delay = rng.uniform(.002, .055) if shard < 16 else rng.uniform(.07, .47)
        age = np.maximum(0, t - delay)
        active = t >= delay
        strength = rng.uniform(.045, .095) if shard < 16 else rng.uniform(.009, .034)
        hit = rng.normal(0, 1, len(t))
        hit -= np.concatenate(([0], hit[:-1])) * .9
        audio += strength * hit * active * np.exp(-age / rng.uniform(.0012, .0028))
        fundamental = rng.uniform(2200, 6400)
        for ratio in [1, 1.39, 1.91, 2.47]:
            frequency = min(fundamental * ratio, 14500)
            decay = rng.uniform(.018, .075) * (fundamental / frequency) ** .6
            phase = rng.uniform(0, np.pi * 2)
            tone = np.sin(2 * np.pi * frequency * age + phase)
            onset = 1 - np.exp(-age / .00012)
            audio += tone * active * onset * np.exp(-age / decay) * strength * .42 / ratio
    audio *= np.minimum(1, t / .0001)
    audio *= np.clip((length - t) / .07, 0, 1)
    audio = .92 * audio / max(.001, np.max(np.abs(audio)))
    samples = (np.clip(audio, -1, 1) * 32767).astype('<i2')
    with wave.open(str(folder / f'BottleBreak0{variant}.wav'), 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(rate)
        output.writeframes(samples.tobytes())
print('Saved three original glass fracture effects')
