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
Backend/          Firebase (Phase 7)
Server/           Dedicated server deployment (Phase 4–5)
ArtSource/        Blender sources (Phase 2)
Docs/             Documentation
```

## Requirements

* Unity Hub + Unity **6000.3.x LTS** (Android, iOS, Linux Dedicated Server modules)
* Git + **Git LFS** (`git lfs install` once per machine)
* macOS + Xcode for iOS builds
