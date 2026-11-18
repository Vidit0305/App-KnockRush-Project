#!/usr/bin/env bash
# KnockRush local load test: 1 headless dedicated server + N headless bot clients on this PC.
#
#   Tools/loadtest.sh <PHOTON_APP_ID> [bots=31] [seconds=120] [session=loadtest-<time>]
#
# Requires the Windows development player (Tools/unity.sh windows) and a Photon Fusion App ID.
# Every process is a separate headless player (~150-250 MB RAM each); 31 bots + server need
# roughly 6-8 GB RAM. Use fewer bots on smaller machines, or run bots on several PCs with the
# same session name. Logs: .logs/loadtest/.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
EXE="$ROOT/KnockRush/Builds/Windows/KnockRush.exe"
APP_ID="${1:?usage: Tools/loadtest.sh <PHOTON_APP_ID> [bots] [seconds] [session]}"
BOTS="${2:-31}"
SECONDS_TO_RUN="${3:-120}"
SESSION="${4:-loadtest-$(date +%H%M%S)}"
LOGS="$ROOT/.logs/loadtest"
mkdir -p "$LOGS"
rm -f "$LOGS"/*.log

echo "[loadtest] server + $BOTS bots, session '$SESSION', ${SECONDS_TO_RUN}s"
"$EXE" -batchmode -nographics -server -session "$SESSION" -photonAppId "$APP_ID" \
  -quitAfterSeconds $((SECONDS_TO_RUN + 30)) -logFile "$LOGS/server.log" &
SERVER_PID=$!
sleep 15

for i in $(seq 1 "$BOTS"); do
  "$EXE" -batchmode -nographics -botclient -session "$SESSION" -photonAppId "$APP_ID" \
    -quitAfterSeconds "$SECONDS_TO_RUN" -logFile "$LOGS/bot-$i.log" &
  sleep 0.5
done

wait "$SERVER_PID" || true
sleep 5

spawned=$(grep -c "Spawned character" "$LOGS/server.log" || true)
errors=$(grep -lE "Exception|\\[Error\\]" "$LOGS"/*.log 2>/dev/null | wc -l || true)
connected=$(grep -l "Connected to server" "$LOGS"/bot-*.log 2>/dev/null | wc -l || true)
echo "[loadtest] characters spawned by server: $spawned / $BOTS"
echo "[loadtest] bots connected: $connected / $BOTS"
echo "[loadtest] logs with errors: $errors"
grep -hE "Session shut down|Disconnected|Connect failed" "$LOGS"/*.log | sort | uniq -c | head -20
[ "$spawned" -ge "$BOTS" ] && [ "$errors" -eq 0 ]
