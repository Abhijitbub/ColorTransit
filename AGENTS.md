# Color Transit Flow

- Android is the delivery platform. Do not substitute a Windows build for phone APK and signed Play AAB.
- Read Docs/Status.md and Docs/Publishing.md before continuing. Distinguish verified work from planned or account-dependent steps.
- Unity 6000.3.23f1, ARM64/IL2CPP, min API 26, target API 36. Recheck current requirements for later releases.
- Run `dotnet run --project Tests/TransitChecks.csproj` after core/save-model changes. Exercise Android builds after runtime changes.
- `Tools/Build-Android.ps1 -Target Deliverables` builds offline signed AAB, phone APK and a signed x86 emulator test variant. Keep private signing material out of source control and uploads. Never print passwords or overwrite an existing upload key.
- The public offline edition has live/test ads disabled, no internet/advertising identifier permissions and no automatic ad provider. Never claim live monetization readiness from sample-ad tests.
- Production QA must include manifest/signature/architecture/16 KB checks and actual rendered Android screens. Hidden Windows screenshots are not Android evidence.
- Artwork and original music sources are local; see Docs/Asset-Provenance.md. Preserve source assets and repeatable generation/build scripts.
