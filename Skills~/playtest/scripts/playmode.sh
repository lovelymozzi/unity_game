#!/usr/bin/env bash
# playtest: Play Mode 진입(on)/종료(off) + 도메인 리로드 재연결 폴링.
# poll: 3s 간격, 최대 40회(~125s). exit 0=원하는 state 도달, 2=timeout.
set -euo pipefail
MODE="${1:-on}"
DIR="$(cd "$(dirname "$0")/../../unity-editor-ops/scripts" && pwd)"
UEXEC="$DIR/uexec.sh"
PORT="$("$DIR/resolve-port.sh")"
if [ "$MODE" = "on" ]; then WANT="playing"; VAL="true"; else WANT="ready"; VAL="false"; fi
"$UEXEC" "UnityEditor.EditorApplication.isPlaying = $VAL; return \"requested\";" >/dev/null 2>&1 || true
sleep 4
for i in $(seq 1 40); do
  S=$(curl -s --max-time 3 "http://127.0.0.1:$PORT/status" 2>/dev/null || true)
  if echo "$S" | grep -q "\"state\":\"$WANT\""; then echo "STATE=$WANT (i=$i)"; exit 0; fi
  sleep 3
done
echo "TIMEOUT waiting for $WANT"; exit 2
