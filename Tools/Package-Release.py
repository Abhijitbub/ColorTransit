"""Package only explicitly selected public artifacts; never signing material."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import hashlib, struct
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Builds'/'Release'; OUT.mkdir(parents=True,exist_ok=True)
artifacts=[ROOT/'Builds/Android/ColorTransitFlow-1.0.0-v1.aab',ROOT/'Builds/Android/ColorTransitFlow-1.0.0.apk']
checks=''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  Android/'+p.name+'\n' for p in artifacts)
(OUT/'SHA256SUMS.txt').write_text(checks)
for p in (ROOT/'StoreAssets/Upload').glob('*.png'):
    width,height=struct.unpack('>II',p.read_bytes()[16:24])
    expected=(512,512) if p.name.startswith('Icon') else (1024,500) if p.name.startswith('Feature') else (1080,1920)
    assert (width,height)==expected,(p.name,width,height)
with ZipFile(OUT/'ColorTransitFlow-1.0.0-Publishing.zip','w',ZIP_DEFLATED) as z:
    for p in artifacts: z.write(p,'Android/'+p.name)
    for p in sorted((ROOT/'StoreAssets/Upload').iterdir()):
        if p.is_file(): z.write(p,'StoreAssets/Upload/'+p.name)
    for p in sorted((ROOT/'Website').glob('*.html')): z.write(p,'Website/'+p.name)
    for name in ['Publishing.md','Release-Verification.md','Monetization.md','Asset-Provenance.md']:
        z.write(ROOT/'Docs'/name,'Docs/'+name)
    z.write(OUT/'SHA256SUMS.txt','SHA256SUMS.txt')
    z.writestr('START-HERE.txt','Color Transit Flow 1.0.0 — JSCA Games\n\nRead Docs/Publishing.md. Android contains the signed ARM64 phone APK and Play AAB. StoreAssets/Upload contains final art, four actual Android screenshots and listing copy. Website contains the privacy/support HTML to publish on the existing JSCA Games site.\n\nThis edition is offline with ads disabled. Live monetization requires app-specific AdMob IDs and verified consent configuration, then a new build. Complete physical-phone and Play internal testing before production submission. Verification evidence and boundaries are recorded in Docs/Release-Verification.md.\n\nKeep the original project and its private .signing directory securely backed up. Signing credentials are deliberately not included in this publishing archive.\n')
with ZipFile(OUT/'ColorTransitFlow-1.0.0-Publishing.zip') as z:
    assert z.testzip() is None
    assert not any(any(s in n.lower() for s in ['.signing','.jks','password','qa-x86','ad-test']) for n in z.namelist())
    assert len(z.namelist())==18,len(z.namelist())
print('PASS: store image dimensions; 18-file publishing archive integrity; explicit public-file allowlist; SHA-256 manifest.')
print(OUT/'ColorTransitFlow-1.0.0-Publishing.zip')
