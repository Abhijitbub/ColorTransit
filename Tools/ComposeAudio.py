"""Original deterministic music and effects; no samples or third-party recordings."""
from pathlib import Path
import numpy as np
import wave

OUT = Path(__file__).resolve().parents[1] / 'Assets/ColorTransit/Resources/Audio'
OUT.mkdir(parents=True, exist_ok=True)
SR = 32000
def write(name, data):
    peak = np.max(np.abs(data))
    if peak > .93: data *= .93 / peak
    with wave.open(str(OUT / (name + '.wav')), 'wb') as f:
        f.setnchannels(2); f.setsampwidth(2); f.setframerate(SR)
        f.writeframes((data * 32767).astype('<i2').tobytes())
def freq(n): return 440 * 2 ** ((n - 69) / 12)
beat = 60 / 96
bars = 32
music = np.zeros((int(SR * beat * 4 * bars), 2), dtype=np.float64)
rng = np.random.default_rng(811)
def put(at, n, length, amp, pan=0, voice='keys'):
    t = np.arange(int(length * SR)) / SR
    f = freq(n)
    if voice == 'keys':
        a = (np.sin(2*np.pi*f*t) + .22*np.sin(2*np.pi*f*2*t) + .08*np.sin(2*np.pi*f*3*t)) * np.exp(-t*3.4)
    elif voice == 'pad':
        a = (np.sin(2*np.pi*f*t) + .25*np.sin(2*np.pi*f*1.003*t)) * np.sin(np.pi*np.minimum(t/length,1))**2
    elif voice == 'bass':
        a = (np.sin(2*np.pi*f*t)+.12*np.sin(2*np.pi*f*2*t))*np.exp(-t*2)
    else:
        a = rng.normal(0,.22,len(t)) * np.exp(-t*42)
    a *= np.minimum(t*80,1)*np.minimum((length-t)*25,1)*amp
    start = int(at * SR)
    stereo = np.column_stack((a*(1-pan*.4), a*(1+pan*.4)))
    # Wrap decay tails around the end for a seamless loop.
    indices = (start + np.arange(len(t))) % len(music)
    music[indices] += stereo
chords = [(48,55,59,62),(45,52,55,59),(41,48,52,57),(43,50,55,59)]
melodies = [[72,74,76,79,76,74,71,74],[72,71,69,67,69,71,74,72],[69,72,76,74,72,69,67,64],[67,71,74,79,77,74,71,67]]
for bar in range(bars):
    base = bar*4*beat; chord = chords[bar%4]
    for j,n in enumerate(chord): put(base,n,4*beat,.045,(j-1.5)/2,'pad')
    for step in range(8): put(base+step*.5*beat,chord[(step+bar//8)%4]+12,1.1,.09,(-1)**step*.45)
    for step in [0,2]: put(base+step*beat,chord[0]-12,.9,.18,0,'bass')
    if bar%8 >= 2:
        for k in range(4): put(base+(k+.25)*beat,melodies[bar%4][(k+bar//4)%8],.95,.11,.15)
    for step in range(8): put(base+step*.5*beat,0,.12,.11 if step%2 else .065,(-1)**step*.65,'hat')
# Short diffuse echoes add space, preserving loop continuity.
music += np.roll(music,int(SR*beat*.75),axis=0)*.13
write('CityAfterglow', music)
for name,notes in {'Tap':[76], 'Board':[81,88], 'Depart':[67,72,79], 'Win':[72,76,79,84], 'Undo':[72,67], 'Jam':[52,48]}.items():
    duration=.18*len(notes)+.35; data=np.zeros((int(SR*duration),2))
    for i,n in enumerate(notes):
        t=np.arange(int(.35*SR))/SR
        a=np.sin(2*np.pi*freq(n)*t)*np.exp(-t*14)*np.minimum(t*150,1)*.32
        j=int(i*.18*SR); data[j:j+len(t),:]+=a[:,None]
    write(name,data)
print('Created original 80-second stereo loop and six sound effects.')
