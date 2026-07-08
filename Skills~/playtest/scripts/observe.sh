#!/usr/bin/env bash
# playtest: 결과 관찰 스냅샷 — 콘솔 에러 + 활성 씬 + isPlaying.
set -euo pipefail
DIR="$(cd "$(dirname "$0")/../../unity-editor-ops/scripts" && pwd)"
UEXEC="$DIR/uexec.sh"
PORT="$("$DIR/resolve-port.sh")"
TOKEN="$(cat "$HOME/.unity-exec/auth-token")"
echo "=== console (error) ==="
curl -s "http://127.0.0.1:$PORT/logs?count=20&level=error" -H "X-Auth-Token: $TOKEN"
echo ""
echo "=== state ==="
"$UEXEC" "return new { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, isPlaying = UnityEditor.EditorApplication.isPlaying };"
