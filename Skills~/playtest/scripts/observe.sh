#!/usr/bin/env bash
# playtest: 결과 관찰 스냅샷 — 콘솔 에러 + 활성 씬 + isPlaying.
set -euo pipefail
. "$(cd "$(dirname "$0")" && pwd)/_cli.sh"
echo "=== console (error) ==="
ucmd get_console_logs --severity error --limit 20 | jq -c '.data.result' 2>/dev/null || true
echo ""
echo "=== state ==="
ev 'return new { scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, isPlaying = UnityEditor.EditorApplication.isPlaying };'
