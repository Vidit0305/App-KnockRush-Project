#!/usr/bin/env bash
# Runs the full backend verification on the Firebase Emulator Suite:
#   1. Cloud Functions unit tests (pure rules)
#   2. Functions + Firestore rules integration tests
#   3. Unity PlayMode tests (incl. the Unity REST client against the emulators)
# Usage: Tools/backend-test.sh [--skip-unity]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT/Backend/firebase/functions"

# Windows: firebase emulators:exec can leave the Firestore emulator's Java process running,
# which blocks port 8080 for the next run. Stop any orphaned emulator first.
stop_orphans() {
  if command -v powershell >/dev/null 2>&1; then
    powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name = 'java.exe'\" | Where-Object { \$_.CommandLine -match 'cloud-firestore-emulator' } | ForEach-Object { Stop-Process -Id \$_.ProcessId -Force }" || true
  fi
}
stop_orphans
[ -d node_modules ] || npm install
npm run pretest --silent
npm run test:unit
npx firebase emulators:exec --project demo-knockrush --only auth,firestore,functions "npm run test:integration"
stop_orphans
if [ "${1:-}" != "--skip-unity" ]; then
  npx firebase emulators:exec --project demo-knockrush --only auth,firestore,functions "bash $ROOT/Tools/unity.sh playmode"
fi
