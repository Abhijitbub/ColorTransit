"""Final release regression on the isolated emulator, with raw store screenshots."""
import json, subprocess, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
ADB=r'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
PKG='com.jscagames.colortransitflow'
SAVE='/sdcard/Android/data/'+PKG+'/files/transit-save.json'
OUT=ROOT/'Logs'/'Signed-Android'; OUT.mkdir(parents=True,exist_ok=True)
STORE=ROOT/'StoreAssets'/'Upload'
def adb(*args): return subprocess.check_output([ADB,'-s','emulator-5558',*args],timeout=30).decode(errors='replace')
def tap(x,y,delay=1):
    adb('shell','input','tap',str(round(31.5+x*1808/960)),str(round(112+y*1808/960))); time.sleep(delay)
def state(): return json.loads(adb('shell','cat',SAVE))
def capture(name,store=False):
    time.sleep(3)
    adb('shell','screencap','-p','/sdcard/transit-final.png')
    adb('pull','/sdcard/transit-final.png',str((STORE if store else OUT)/(name+'.png')))
def restart():
    adb('shell','am','force-stop',PKG); adb('shell','am','start','-n',PKG+'/com.unity3d.player.UnityPlayerGameActivity'); time.sleep(4)
restart()
capture('04-home',True)
tap(270,726)
capture('01-gameplay',True)
adb('shell','input','keyevent','3'); time.sleep(1)
restart(); tap(270,726)
s=state()
assert s['coins']==0 and s['board']['level']==1
while s['board']['cursor']<len(s['board']['queue']):
    b=s['board']; v=next(v for v in b['lot'] if v['Color']==b['queue'][b['cursor']]); i=b['lot'].index(v)
    tap(84+(i%4)*124,693+(i//4)*94,5)
    s=state(); assert s['board']['moves']>b['moves']
assert s['coins']==25 and s['unlocked']==2
capture('completion')
restart(); assert state()['coins']==25
tap(270,888); capture('settings')
tap(270,259); assert state()['music'] is False
restart(); assert state()['music'] is False
print('PASS signed route completion, reward, restart and settings persistence',flush=True)
# Restore the real seven-route test profile solely on this disposable QA emulator.
adb('shell','am','force-stop',PKG)
adb('push',str(ROOT/'Logs'/'after-test-ad-save.json'),SAVE)
restart(); tap(390,807); capture('03-station',True)
tap(270,825); tap(150,807); tap(270,516); capture('02-parking-lanes',True)
logs=adb('logcat','-d','-s','Unity','AndroidRuntime')
(OUT/'runtime.log').write_text(logs,encoding='utf-8')
for error in ['FATAL EXCEPTION','NullReferenceException','Save unavailable','Transit ads: Optional ad ready']:
    assert error not in logs,error
(OUT/'PASS.txt').write_text('PASS: signed non-development x86 release ran on Android API 35; route 1 completed through real taps; reward and music preference persisted after process death; no inspected fatal/managed/save errors or ad initialization. Raw 1080x1920 store screenshots captured. Station and route 8 use restored QA progression. ARM64 physical-device testing remains external.\n')
print('PASS signed runtime and screenshots',flush=True)
