# Phase 1: Unity Project Setup and Architecture

**Status: complete.** All setup is automated and committed. This guide explains what exists, how to open it, and how to re-verify it.

## 1. What we built

* Unity **6000.3.28f1 (6.3 LTS)** project `KnockRush/`, created from the **Universal 3D** (URP) template, configured for Android/iOS landscape mobile.
* The **folder structure** and **assembly definitions** from [ARCHITECTURE.md](ARCHITECTURE.md).
* The **application core**: `AppRoot` (composition root), `ServiceLocator`, `EventBus`, `SceneLoader`, `GameConfig`, `LocalSettingsService`, `PlatformSettings`, `DebugOverlay`.
* A **Boot → MainMenu** flow with placeholder visuals (replaced in Phase 8).
* **Editor tooling:** `KnockRush > Setup` (idempotent setup + validator) and `KnockRush > Build` (Android development APK). Both are also callable from the command line.
* **Tests:** 17 EditMode unit tests and 3 PlayMode milestone tests.

## 2. Why it is required

Every later system (movement, networking, backend, UI) is created and resolved through this core. Locking in the platform settings now (IL2CPP, ARM64, landscape, linear colour, URP, Input System) avoids rework, and assemblies keep compile times and dependencies under control.

## 3. Software and packages

| Item | Version |
|---|---|
| Unity Editor | **6000.3.28f1** with Android Build Support (OpenJDK + SDK/NDK). iOS and Linux Dedicated Server modules are added when needed (Phase 4 / 10). |
| Git + Git LFS | `git lfs install` once per machine before cloning |
| Universal RP | 17.3.0 (**embedded** in `Packages/`, see [ARCHITECTURE.md §14](ARCHITECTURE.md)) |
| Input System | 1.20.1 |
| Addressables | 2.10.3 |
| Test Framework | 1.6.0 |

## 4. Opening the project on any machine

1. `git lfs install`, then `git clone https://github.com/Vidit0305/App-KnockRush-Project.git`
2. Unity Hub → **Projects → Add → Add project from disk** → select the `KnockRush` folder.
3. Open with **6000.3.28f1**. The first import takes a few minutes.
4. Run **KnockRush → Setup → Validate Project**: all checks should pass.

## 5. Scenes and GameObjects

| Scene | GameObject | Components |
|---|---|---|
| `Assets/Scenes/Boot.unity` (index 0) | Main Camera | Camera, Solid Color `#5B2EFF` |
| | Directional Light | Light |
| | BootSceneController | `BootSceneController` |
| `Assets/Scenes/MainMenu.unity` (index 1) | Main Camera, Directional Light | Solid Color `#1E145C` |
| | MainMenuPlaceholder | `MainMenuPlaceholder` |
| *Runtime only* | `[KnockRush App]` (DontDestroyOnLoad) | `AppRoot`, `ApplicationLifecycleRelay`, `DebugOverlay` (dev builds) |

`[KnockRush App]` is never placed in a scene. `AppRoot` creates it before the first scene loads, so **Play works from any scene**.

## 6. Configuration

`Assets/ScriptableObjects/Config/Resources/GameConfig.asset`: Environment `Development`, Target Frame Rate `60`, First Scene After Boot `MainMenu`, Minimum Splash Duration `1.5`, Show Debug Overlay ✔.

Player settings applied by the setup tool:

| Setting | Value |
|---|---|
| Company / Product | KnockRush Studio / KnockRush |
| Application identifier | `com.knockrushstudio.knockrush` (Android and iOS). Change it in `KnockRushProjectSetup.ApplicationIdentifier` + Player Settings **before Phase 7** if you want a different one. |
| Version | 0.1.0 |
| Scripting backend | IL2CPP (Android, iOS) |
| Android architectures | ARM64 |
| Orientation | Auto-rotation, landscape left/right only |
| Colour space | Linear |
| Active Input Handling | Input System Package (New) |
| Quality default on Android | Mobile (`Mobile_RPAsset`) |
| Asset serialization / meta files | Force Text / Visible |

## 7. Scripts

```
Assets/Scripts/
├── Utilities/  KnockRush.Utilities.asmdef, Log.cs
├── Core/       KnockRush.Core.asmdef, AppRoot.cs
│   ├── Config/           BuildEnvironment.cs, GameConfig.cs
│   ├── Services/         IGameService.cs, ServiceLocator.cs
│   ├── Events/           EventBus.cs, CoreEvents.cs
│   ├── SceneManagement/  SceneNames.cs, ISceneLoader.cs, SceneLoader.cs, BootSceneController.cs
│   ├── Settings/         LocalSettings.cs, LocalSettingsService.cs
│   ├── Platform/         PlatformSettings.cs, ApplicationLifecycleRelay.cs
│   └── Diagnostics/      DebugOverlay.cs
├── UI/         KnockRush.UI.asmdef, Placeholders/MainMenuPlaceholder.cs
└── Editor/     KnockRush.Editor.asmdef
    ├── Setup/  KnockRushProjectSetup.cs, KnockRushProjectValidator.cs
    └── Build/  KnockRushBuild.cs
Assets/Tests/
├── EditMode/   ServiceLocatorTests.cs, EventBusTests.cs, LocalSettingsTests.cs
└── PlayMode/   BootFlowTests.cs
```

## 8. How it connects

1. On Play / app launch, `AppRoot.Bootstrap()` runs **before the first scene loads**.
2. `AppRoot` loads `GameConfig`, applies `PlatformSettings`, registers `GameConfig`, `ILocalSettingsService` and `ISceneLoader`, initialises them in order, and publishes `AppInitializedEvent`.
3. `BootSceneController` awaits `AppRoot.InitializationTask`, keeps the splash for the minimum duration, then loads `MainMenu` through `ISceneLoader`.
4. `MainMenuPlaceholder` resolves services; saved settings go through `LocalSettingsService` to PlayerPrefs and fire `LocalSettingsChangedEvent`.

## 9. Verification (all automated)

`<Unity>` = path to `Unity.exe` 6000.3.28f1. Run from the repository root.

| Check | Command | Result (2026-11-18) |
|---|---|---|
| Setup + validation | `<Unity> -batchmode -projectPath KnockRush -executeMethod KnockRush.EditorTools.Setup.KnockRushProjectSetup.RunBatchSetup -logFile setup.log` | exit 0, **all checks pass** |
| EditMode tests | `<Unity> -batchmode -projectPath KnockRush -runTests -testPlatform EditMode -assemblyNames KnockRush.Tests.EditMode -testResults editmode.xml` | **17/17 passed** |
| PlayMode milestone tests | `<Unity> -batchmode -projectPath KnockRush -runTests -testPlatform PlayMode -assemblyNames KnockRush.Tests.PlayMode -testResults playmode.xml` | **3/3 passed** (AppRoot boot, Boot → MainMenu, settings persistence) |
| Android development APK | `<Unity> -batchmode -projectPath KnockRush -buildTarget Android -executeMethod KnockRush.EditorTools.Build.KnockRushBuild.BuildAndroidDevelopmentBatch -logFile build.log` | **Succeeded**: `KnockRush/Builds/Android/KnockRush-dev.apk`, 50.8 MB, 0 errors (about 14 min for the first IL2CPP build) |

Manual check in the editor: open `Boot.unity` and press Play. You should see the purple "KNOCKRUSH" splash, then after 1.5 s the MainMenu placeholder, with the FPS overlay at top-left. Saving the sensitivity slider persists across Play sessions.

On a device: install `KnockRush/Builds/Android/KnockRush-dev.apk` (`adb install -r …`). Expect landscape only, splash → MainMenu, and the overlay at about 60 FPS.

## 10. Common problems and fixes

| Symptom | Cause | Fix |
|---|---|---|
| `CS0246 MaterialReferenceChanger` in URP | Built-in URP in 6000.3.28f1 is missing two `.meta` files | Keep the embedded URP in `Packages/` (already done). |
| `KnockRush` menu missing | Compile errors | Open the Console and fix the first error. |
| `GameConfig asset not found` | Asset moved out of `Resources` | Run Setup step 4; keep exactly one `GameConfig.asset` in a `Resources` folder. |
| `Scene 'MainMenu' is not in Build Settings` | Scene list edited | Run Setup step 4 again (idempotent). |
| Pink materials | URP asset unassigned | Project Settings → Graphics → Default Render Pipeline = `PC_RPAsset`; Quality → Mobile uses `Mobile_RPAsset`. |
| Overlay missing | Release build | Tick *Development Build*. |
| Android build: SDK/NDK/JDK not found | Android module incomplete | Unity Hub → Installs → Add modules → Android SDK & NDK Tools + OpenJDK. |
| Large/binary files show as pointers after clone | Git LFS not installed | `git lfs install && git lfs pull`. |
