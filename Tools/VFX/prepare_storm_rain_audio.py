from pathlib import Path
import json
import re
import urllib.request
import sys
import numpy as np

root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'Library/StormRainAudioTools'))
import soundfile as sf
source_dir=root/'Art/Audio/StormRain'
target_dir=root/'Assets/Audio/Ambience/Storm'
source_dir.mkdir(parents=True,exist_ok=True)
target_dir.mkdir(parents=True,exist_ok=True)
sources=[
    ('RainDownpourLoop','https://freesound.org/people/lebaston100/sounds/346562/',15,120,'CC BY 4.0','lebaston100'),
    ('RainDeckLoop','https://freesound.org/people/Breviceps/sounds/484724/',5,250,'CC0','Breviceps'),
]
manifest=[]
for name,url,start,highpass,license_name,author in sources:
    request=urllib.request.Request(url,headers={'User-Agent':'PirateSlop audio asset preparation'})
    html=urllib.request.urlopen(request,timeout=25).read().decode()
    links=re.findall(r'https://[^\s"<>]+-hq\.(?:ogg|mp3)',html)
    if not links:
        raise RuntimeError('No public high quality preview found: '+url)
    preview=links[0].replace('&amp;','&')
    original=source_dir/(name+'-source'+Path(preview).suffix)
    if not original.exists():
        original.write_bytes(urllib.request.urlopen(preview,timeout=30).read())
    samples,rate=sf.read(original,always_2d=True)
    duration=25
    samples=samples[int(start*rate):int((start+duration)*rate)]
    if len(samples)<duration*rate:
        raise RuntimeError('Source shorter than selected interval')
    frequencies=np.fft.rfftfreq(len(samples),1/rate)
    spectrum=np.fft.rfft(samples,axis=0)
    spectrum*=((frequencies**2)/(frequencies**2+highpass**2))[:,None]
    samples=np.fft.irfft(spectrum,n=len(samples),axis=0)
    cross=rate
    blend=np.linspace(0,1,cross,endpoint=False)[:,None]
    seam=samples[-cross:]*(1-blend)+samples[:cross]*blend
    loop=np.concatenate([seam,samples[cross:-cross]])
    rms=np.sqrt(np.mean(loop**2))
    loop*=min(.20/max(rms,1e-9),.87/max(np.max(np.abs(loop)),1e-9))
    target=target_dir/(name+'.wav')
    sf.write(target,loop,rate,subtype='PCM_16')
    manifest.append(dict(asset=str(target.relative_to(root)),source=url,preview=preview,
        author=author,license=license_name,interval=[start,start+duration],
        crossfade_seconds=1,loop_seconds=len(loop)/rate,samplerate=rate,
        rms=float(np.sqrt(np.mean(loop**2))),peak=float(np.max(np.abs(loop)))))
(source_dir/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=False))
