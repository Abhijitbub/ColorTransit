"""Touch/lifecycle regression for the project's isolated 1080x2340 emulator.
Run with the game on its completed route-1 screen. Does not touch other apps.
"""
import json, subprocess, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
ADB=r'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
PACKAGE='com.jscagames.colortransitflow'
ACTIVITY=PACKAGE+'/com.unity3d.player.UnityPlayerGameActivity'
SAVE='/sdcard/Android/data/'+PACKAGE+'/files/transit-save.json'
OUT=ROOT/'Logs'/'Android-Input'; OUT.mkdir(parents=True,exist_ok=True)
def adb(*args):
    return subprocess.check_output([ADB,'-s','emulator-5558',*args],timeout=30).decode(errors='replace')
def tap(x,y,delay=1):
    adb('shell','input','tap',str(round(x*2)),str(round(280+y*2))); time.sleep(delay)
def save(): return json.loads(adb('shell','cat',SAVE))
def capture(name):
    adb('shell','screencap','-p','/sdcard/transit-qa.png'); adb('pull','/sdcard/transit-qa.png',str(OUT/(name+'.png')))
def restart():
    adb('shell','am','force-stop',PACKAGE); adb('shell','am','start','-n',ACTIVITY); time.sleep(4)
def check(ok,message):
    if not ok: raise AssertionError(message)
    print('PASS '+message,flush=True)

adb('wait-for-device')
import sys
if '--resume' not in sys.argv:
    s=save(); check(s['coins']==25 and s['unlocked']==2,'route completion earns exactly 25 coins')
    restart(); check(save()['coins']==25,'cold restart does not duplicate completion reward')
    tap(270,726,1.5)
    if save()['board']['level']==1: tap(270,558,1.5)
    check(save()['board']['level']==2,'Next advances to route 2')
    tap(466,58); tap(270,259) # pause, music off
    check(save()['music'] is False,'music toggle saves')
    restart(); tap(270,888); capture('settings-persisted')
    check(save()['music'] is False,'music choice survives process death')
    tap(270,259); tap(270,752) # music on, resume
    check(save()['music'] is True,'music can be restored')
for level in range(save()['board']['level'],8):
    s=save(); check(s['board']['level']==level,'route '+str(level)+' started')
    dispatches=0
    while s['board']['cursor'] < len(s['board']['queue']):
        b=s['board']; lot=b['lot']; color=b['queue'][b['cursor']]
        candidates=[v for v in lot if v['Color']==color and (level<7 or not any(o['Id']%3==v['Id']%3 and o['Id']<v['Id'] for o in lot))]
        check(bool(candidates),'matching accessible vehicle at route '+str(level))
        chosen=min(candidates,key=lambda v:v['Id']); index=lot.index(chosen)
        if index>=8: raise AssertionError('Test route exceeded visible yard')
        tap(23+5+(index%4)*124+56,650+(index//4)*94+43,5)
        updated=save(); check(updated['board']['moves']>b['moves'],'touch dispatch advances state')
        s=updated; dispatches+=1
        if dispatches>18: raise AssertionError('Route did not finish')
    check(s['unlocked']==level+1 and s['coins']==level*25,'reward and unlock at route '+str(level))
    capture('route-'+str(level)+'-complete')
    tap(270,558)
capture('route-8-gameplay')
tap(466,58); tap(270,834); tap(390,807) # pause -> home -> station
capture('station-before')
tap(270,719)
check(save()['theme']==1 and save()['coins']==75,'station upgrade spends exactly 100 coins')
capture('station-upgraded')
restart(); check(save()['theme']==1 and save()['coins']==75,'station upgrade persists')
logs=adb('logcat','-d','-s','Unity','AndroidRuntime'); (OUT/'runtime.log').write_text(logs,encoding='utf-8')
check('NullReferenceException' not in logs and 'Save unavailable' not in logs,'no managed or save errors in inspected log')
(OUT/'PASS.txt').write_text('PASS: Android taps through seven routes, lifecycle recovery, unique completion rewards, music persistence, parking lanes and station upgrade.\n',encoding='utf-8')

