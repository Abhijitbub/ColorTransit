# Color Transit Flow — Android 1.0.0

A portrait offline bus-sorting puzzle built in Unity 6000.3.23f1. Match the passenger queue, manage three boarding bays, clear parking lanes and earn coins to upgrade your station. Includes 10,000 deterministic routes, free undo/hints/retry, local save recovery, original artwork, music, sound effects and persistent settings.

## Delivery

- Install on an ARM64 Android phone: `Builds/Android/ColorTransitFlow-1.0.0.apk`.
- Upload to Play internal testing: `Builds/Android/ColorTransitFlow-1.0.0-v1.aab`.
- Publishing instructions: `Docs/Publishing.md`.
- Build/test evidence: `Docs/Release-Verification.md`.
- Store assets: `StoreAssets/Upload`. Privacy/support pages: `Website`.

The supplied release is offline. Live advertising is disabled; the separate sample rewarded-ad test passed. Live AdMob requires app-specific account IDs and consent verification before another build. Publication and physical ARM64/Play-delivered testing remain publisher steps.

## Development

Open this project in Unity 6000.3.23f1 and play `Assets/ColorTransit/Scenes/Transit.unity`. Core rules are in `TransitModel.cs`; the UI is in `TransitGame.cs` and `TransitScreens.cs`.

Run `dotnet run --project Tests/TransitChecks.csproj` for the 1,000-route rules/save regression. Build signed outputs with `Tools/Build-Android.ps1 -Target Deliverables`, then run `Tools/Verify-Release.ps1`. Android targets API 36, minimum API 26, using IL2CPP and ARM64. The x86 APK is exclusively for emulator QA.

Keep `.signing` private and securely backed up. Never regenerate the upload key for an update. See `Docs/Asset-Provenance.md` for original asset sources and third-party software notices.
