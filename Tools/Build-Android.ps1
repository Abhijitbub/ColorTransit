param([ValidateSet('Release','Phone','Emulator','SignedEmulator','AdTest','Deliverables')][string]$Target = 'Release', [ValidateSet('Offline','Live')][string]$Mode = 'Offline', [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$Emulator = $Target -in @('Emulator','SignedEmulator','AdTest')
$signingDir = Join-Path $projectRoot '.signing'
$keyPath = Join-Path $signingDir 'colortransitflow-upload.jks'
$credentialPath = Join-Path $signingDir 'upload-password.xml'
$keytool = Join-Path (Split-Path $UnityEditor -Parent) 'Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe'
if (!(Test-Path -LiteralPath $UnityEditor) -or !(Test-Path -LiteralPath $keytool)) { throw 'Unity Android tools are missing.' }
$newSigningDirectory = !(Test-Path -LiteralPath $signingDir)
New-Item -ItemType Directory -Force -Path $signingDir | Out-Null
# Restrict local signing material to this Windows user and SYSTEM. Never print the password.
$acl = [System.Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
foreach ($sid in @($identity, [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
    $rule = [System.Security.AccessControl.FileSystemAccessRule]::new($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule)
}
if ($newSigningDirectory) { Set-Acl -LiteralPath $signingDir -AclObject $acl }
else {
    $existingAcl = Get-Acl -LiteralPath $signingDir
    if (!$existingAcl.AreAccessRulesProtected) { throw 'Signing directory must have private, non-inherited access rules.' }
    foreach ($entry in $existingAcl.Access) {
        $entrySid = $entry.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
        if ($entry.AccessControlType -eq 'Allow' -and $entrySid -notin @($identity.Value, 'S-1-5-18')) {
            throw 'Signing directory grants access to an unexpected identity; review its permissions locally.'
        }
    }
}
if ((Test-Path -LiteralPath $keyPath) -ne (Test-Path -LiteralPath $credentialPath)) {
    throw 'Signing material is incomplete. Restore the existing pair; do not overwrite an upload key.'
}
if (!(Test-Path -LiteralPath $keyPath)) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $generated = [Convert]::ToBase64String($bytes)
    $secure = ConvertTo-SecureString $generated -AsPlainText -Force
    $credential = [System.Management.Automation.PSCredential]::new('colortransitflow-upload', $secure)
    $credential | Export-Clixml -LiteralPath $credentialPath
    $env:TRANSIT_UPLOAD_KEY_PASSWORD = $generated
    try {
        & $keytool -genkeypair -keystore $keyPath -storetype JKS -alias colortransitflow-upload -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=JSCA Games, O=JSCA Games' -storepass:env TRANSIT_UPLOAD_KEY_PASSWORD -keypass:env TRANSIT_UPLOAD_KEY_PASSWORD -noprompt
        if ($LASTEXITCODE -ne 0) { throw 'Upload key generation failed.' }
    } finally { $generated = $null; $env:TRANSIT_UPLOAD_KEY_PASSWORD = $null }
}
$credential = Import-Clixml -LiteralPath $credentialPath
$logDir = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logPath = Join-Path $logDir ('build-' + $Target.ToLowerInvariant() + '.log')
$configPath = Join-Path $projectRoot 'Assets/ColorTransit/Resources/Monetization.json'
$originalConfig = Get-Content -LiteralPath $configPath -Raw
try {
    $env:TRANSIT_UPLOAD_KEY_PASSWORD = $credential.GetNetworkCredential().Password
    $env:TRANSIT_UPLOAD_KEY_PATH = $keyPath
    $env:TRANSIT_OFFLINE_RELEASE = if ($Target -eq 'AdTest' -or $Mode -eq 'Live') { '0' } else { '1' }
    if ($Mode -eq 'Offline' -and $Target -ne 'AdTest') {
        $offlineConfig = $originalConfig | ConvertFrom-Json
        $offlineConfig.liveEnabled = $false
        $offlineConfig.testEnabled = $false
        $offlineConfig | ConvertTo-Json | Set-Content -LiteralPath $configPath
    }
    if ($Target -eq 'AdTest') {
        $testConfig = $originalConfig | ConvertFrom-Json
        $testConfig.liveEnabled = $false
        $testConfig.testEnabled = $true
        $testConfig | ConvertTo-Json | Set-Content -LiteralPath $configPath
    }
    $env:TRANSIT_RELEASE_EMULATOR = if ($Emulator) { '1' } else { '0' }
    $arguments = '-batchmode -quit -buildTarget Android -projectPath "' + $projectRoot + '" -executeMethod ColorTransit.Editor.BuildGame.Android' + $Target + ' -logFile "' + $logPath + '"'
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Release build failed; inspect $logPath" }
    if ($Target -in @('Release','Deliverables')) {
        # Normalize JAR signature-entry ordering for both streaming and random-access verifiers.
        $jarSigner = Join-Path (Split-Path $keytool -Parent) 'jarsigner.exe'
        $bundlePath = Join-Path $projectRoot 'Builds/Android/ColorTransitFlow-1.1.0-v2.aab'
        & $jarSigner -keystore $keyPath -storepass:env TRANSIT_UPLOAD_KEY_PASSWORD -keypass:env TRANSIT_UPLOAD_KEY_PASSWORD -sigalg SHA256withRSA -digestalg SHA-256 $bundlePath colortransitflow-upload 2>&1 | Set-Content (Join-Path $logDir 'release-signing.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Release signature normalization failed.' }
    }
    Write-Output "Android $Mode build finished. Back up .signing securely; credentials are Windows-user protected."
} finally {
    Set-Content -LiteralPath $configPath -Value $originalConfig -NoNewline
    $env:TRANSIT_UPLOAD_KEY_PASSWORD = $null
    $env:TRANSIT_UPLOAD_KEY_PATH = $null
    $env:TRANSIT_OFFLINE_RELEASE = $null
    $env:TRANSIT_RELEASE_EMULATOR = $null
    $credential = $null
}

