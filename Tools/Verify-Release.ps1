param([string]$Bundle = 'Builds/Android/ColorTransitFlow-1.1.0-v2.aab', [ValidateSet('Offline','Live')][string]$Mode = 'Live', [int]$VersionCode = 2)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bundlePath = (Resolve-Path -LiteralPath (Join-Path $root $Bundle)).Path
$androidTools = 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/PlaybackEngines/AndroidPlayer'
$java = Join-Path $androidTools 'OpenJDK/bin/java.exe'
$jarSigner = Join-Path $androidTools 'OpenJDK/bin/jarsigner.exe'
$bundletool = Join-Path $androidTools 'Tools/bundletool-all-1.17.2.jar'
$output = Join-Path $root "Logs/Release-Verification-v$VersionCode"
New-Item -ItemType Directory -Force -Path $output | Out-Null
& $java -jar $bundletool validate "--bundle=$bundlePath" 2>&1 | Set-Content (Join-Path $output 'bundle-validation.txt')
if ($LASTEXITCODE -ne 0) { throw 'Bundletool validation failed.' }
$manifestText = & $java -jar $bundletool dump manifest "--bundle=$bundlePath" --module=base
if ($LASTEXITCODE -ne 0) { throw 'Bundle manifest extraction failed.' }
$manifestText | Set-Content (Join-Path $output 'manifest.xml')
[xml]$manifest = $manifestText -join "`n"
$ns = 'http://schemas.android.com/apk/res/android'
$m = $manifest.DocumentElement
if ($m.GetAttribute('package') -ne 'com.jscagames.colortransitflow') { throw 'Wrong package.' }
if ($m.GetAttribute('versionCode', $ns) -ne [string]$VersionCode) { throw 'Wrong version code.' }
$sdk = $m.SelectSingleNode('uses-sdk')
if ($sdk.GetAttribute('minSdkVersion', $ns) -ne '26' -or $sdk.GetAttribute('targetSdkVersion', $ns) -ne '36') { throw 'Wrong SDK targets.' }
$app = $m.SelectSingleNode('application')
if ($app.GetAttribute('debuggable', $ns) -eq 'true') { throw 'Release is debuggable.' }
if ($app.GetAttribute('allowBackup', $ns) -ne 'false') { throw 'Backup is not disabled.' }
foreach ($p in $m.SelectNodes('uses-permission')) {
    $name = $p.GetAttribute('name', $ns)
    if ($Mode -eq 'Offline' -and $name -match 'INTERNET|ACCESS_NETWORK_STATE|AD_ID|AD_SERVICES|ADSERVICES') { throw "Unexpected offline permission: $name" }
}
if ($Mode -eq 'Live') {
    $permissions = @($m.SelectNodes('uses-permission') | ForEach-Object { $_.GetAttribute('name', $ns) })
    foreach ($required in @('android.permission.INTERNET','android.permission.ACCESS_NETWORK_STATE','com.google.android.gms.permission.AD_ID')) {
        if ($required -notin $permissions) { throw "Missing ad permission: $required" }
    }
    $appId = @($app.SelectNodes('meta-data') | Where-Object { $_.GetAttribute('name',$ns) -eq 'com.google.android.gms.ads.APPLICATION_ID' })
    if ($appId.Count -ne 1 -or $appId[0].GetAttribute('value',$ns) -ne 'ca-app-pub-8682564282954420~6277474556') { throw 'Wrong AdMob App ID.' }
}
foreach ($provider in $app.SelectNodes('provider')) {
    if ($provider.GetAttribute('name', $ns) -match 'MobileAdsInitProvider') { throw 'Automatic ad initialization is still present.' }
}
$signature = & $jarSigner -verify $bundlePath 2>&1 | Out-String
$signature | Set-Content (Join-Path $output 'signature.txt')
if ($LASTEXITCODE -ne 0 -or $signature -notmatch 'jar verified') { throw 'Bundle signature verification failed.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($bundlePath)
try {
    $libraries = @($zip.Entries | Where-Object { $_.FullName -match '^base/lib/.+\.so$' })
    if ($libraries.Count -lt 1 -or @($libraries | Where-Object { $_.FullName -notlike 'base/lib/arm64-v8a/*' }).Count) { throw 'Expected ARM64-only libraries.' }
    $readelf = Join-Path $androidTools 'NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/llvm-readelf.exe'
    foreach ($lib in $libraries) {
        $target = Join-Path $output $lib.Name
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($lib, $target, $true)
        $elf = & $readelf -lW $target
        if ($LASTEXITCODE -ne 0) { throw "Cannot inspect $($lib.Name)" }
        $segments = @($elf | Where-Object { $_ -match '^\s+LOAD\s' })
        if (!$segments.Count) { throw "Missing load segments: $($lib.Name)" }
        foreach ($segment in $segments) {
            $alignment = ($segment.Trim() -split '\s+')[-1]
            if ([Convert]::ToInt64($alignment.Replace('0x',''),16) -lt 16384) { throw "Not 16KB aligned: $($lib.Name)" }
        }
        "PASS ELF 16KB alignment: $($lib.Name)" | Add-Content (Join-Path $output 'native-alignment.txt')
    }
} finally { $zip.Dispose() }
$hash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash
"PASS: signed non-debuggable ARM64 bundle; version code $VersionCode; API 26/36; $Mode permissions; no ads auto-init; 16KB ELF alignment.`nSHA256: $hash" | Tee-Object -FilePath (Join-Path $output 'PASS.txt')

