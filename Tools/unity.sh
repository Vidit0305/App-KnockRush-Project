#!/usr/bin/env bash
# KnockRush Unity batch-mode helper (Git Bash / Linux / macOS).
#
#   Tools/unity.sh compile        Open project, compile, exit (fails on compile errors)
#   Tools/unity.sh setup          Apply + validate project setup
#   Tools/unity.sh content        Regenerate generated content (character, prefabs, scenes)
#   Tools/unity.sh editmode       Run KnockRush EditMode tests
#   Tools/unity.sh playmode       Run KnockRush PlayMode tests
#   Tools/unity.sh android        Build the Android development APK
#   Tools/unity.sh windows        Build the Windows development player (headless server/bot testing)
#   Tools/unity.sh server         Build the Linux dedicated server (needs Linux Dedicated Server module)
#   Tools/unity.sh capture <Scene.unity> [Clip]  Render PNG captures to .logs/captures
#   Tools/unity.sh maps           Render overview captures of every map
#   Tools/unity.sh bean           Regenerate the Bean FBX from ArtSource (needs BLENDER_EXE)
#   Tools/unity.sh exec <Method>  Run any static editor method
#
# Set UNITY_EXE to the Unity 6000.3.x editor executable. Logs go to .logs/.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/KnockRush"
LOGS="$ROOT/.logs"
UNITY_EXE="${UNITY_EXE:-D:/Work/Projects/App-DoomShooter-Project/.tools/Unity/6000.3.28f1/Editor/Unity.exe}"
BLENDER_EXE="${BLENDER_EXE:-$ROOT/.tools/blender-4.5.16-windows-x64/blender.exe}"
mkdir -p "$LOGS"

run() {
  local name="$1"; shift
  local log="$LOGS/$name.log"
  echo "[unity.sh] $name -> $log"
  set +e
  "$UNITY_EXE" -batchmode ${GRAPHICS_FLAG--nographics} -projectPath "$PROJECT" -logFile "$log" "$@"
  local code=$?
  set -e
  grep -E "error CS[0-9]+" "$log" | sort -u | head -40 || true
  echo "[unity.sh] $name exit=$code"
  return $code
}

tests() {
  local platform="$1" assembly="$2"
  local results="$LOGS/$platform-results.xml"
  run "$platform" -runTests -testPlatform "$platform" -assemblyNames "$assembly" -testResults "$results" || true
  python - "$results" <<'EOF'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
cases = list(root.iter('test-case'))
failed = [c for c in cases if c.get('result') != 'Passed']
print(f"[unity.sh] {len(cases) - len(failed)}/{len(cases)} passed")
for c in failed:
    msg = (c.findtext('failure/message') or '').strip().replace('\n', ' ')[:400]
    print(f"  FAIL {c.get('fullname')}: {msg}")
sys.exit(1 if failed or not cases else 0)
EOF
}

case "${1:-}" in
  compile)  run compile -quit ;;
  setup)    run setup -executeMethod KnockRush.EditorTools.Setup.KnockRushProjectSetup.RunBatchSetup ;;
  content)  run content -executeMethod KnockRush.EditorTools.Build.ContentBuilder.BuildBatch ;;
  editmode) tests EditMode KnockRush.Tests.EditMode ;;
  playmode) tests PlayMode KnockRush.Tests.PlayMode ;;
  android)  GRAPHICS_FLAG="" run android -buildTarget Android -executeMethod KnockRush.EditorTools.Build.KnockRushBuild.BuildAndroidDevelopmentBatch ;;
  windows)  GRAPHICS_FLAG="" run windows -buildTarget Win64 -executeMethod KnockRush.EditorTools.Build.KnockRushBuild.BuildWindowsDevelopmentBatch ;;
  server)   run server -buildTarget Linux64 -standaloneBuildSubtarget Server -executeMethod KnockRush.EditorTools.Build.KnockRushBuild.BuildLinuxServerBatch ;;
  capture)  GRAPHICS_FLAG="" run "capture" -executeMethod KnockRush.EditorTools.Diagnostics.SceneCapture.CaptureBatch               -captureScene "${2:-Assets/Scenes/Dev/Playground.unity}" -captureOut "$LOGS/captures" -captureClip "${3:-Run}" ;;
  maps)     GRAPHICS_FLAG="" run "capture-maps" -executeMethod KnockRush.EditorTools.Diagnostics.SceneCapture.CaptureAllMapsBatch -captureOut "$LOGS/captures" ;;
  bean)     "$BLENDER_EXE" -b --factory-startup --python "$ROOT/ArtSource/Characters/build_bean.py" --               "$PROJECT/Assets/Art/Characters/Bean/Bean.fbx" "$ROOT/ArtSource/Characters/Bean.blend" | grep build_bean ;;
  exec)     shift; run "exec-${1##*.}" -executeMethod "$1" ;;
  *) sed -n '2,16p' "$0"; exit 2 ;;
esac
