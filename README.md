# KnockRush

Original 32-player online party-knockout game for Android and iOS. Built with Unity 6.3 LTS (URP), Photon Fusion 2 (dedicated server) and Firebase.

| Document | Purpose |
|---|---|
| [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) | Technical architecture and decisions |
| [Docs/DEVELOPMENT_CHECKLIST.md](Docs/DEVELOPMENT_CHECKLIST.md) | Phase-by-phase progress tracker |
| [Docs/PHASE_1_SETUP.md](Docs/PHASE_1_SETUP.md) | Current phase: step-by-step setup guide |

## Repository layout

```
KnockRush/        Unity project
  Assets/Art/       Branding (app icon, logo), Characters, Shaders, UI (Backgrounds, Fonts, Icons, Skins), World
  Assets/Scripts/   Runtime assemblies per feature + Editor builders (content is generated, not hand-placed)
Backend/          Firebase Cloud Functions + rules
Server/           Dedicated server deployment
ArtSource/        Blender character source, UI art generators (ArtSource/UI)
Tools/            unity.sh (batch builds/tests), backend-test.sh
Docs/             Documentation
```

## Building

```
Tools/unity.sh content       # regenerate characters, prefabs, maps, menu and UI assets
Tools/unity.sh android       # Builds/Android/KnockRush.apk  (release configuration, ~37 MB)
Tools/unity.sh android-dev   # Builds/Android/KnockRush-dev.apk (debuggable, much larger)
Tools/unity.sh playmode      # tests (also: editmode)
```

The release APK is signed with the debug key until the store keystore is added (Phase 10).

## Requirements

* Unity Hub + Unity **6000.3.x LTS** (Android, iOS, Linux Dedicated Server modules)
* Git + **Git LFS** (`git lfs install` once per machine)
* macOS + Xcode for iOS builds
