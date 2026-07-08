#!/usr/bin/env bash
# playtest: 게임뷰를 PNG로 캡쳐 (edit/play 모두). 비동기 쓰기 완료까지 폴링.
set -euo pipefail
OUT="${1:-/tmp/uitest/shot.png}"
mkdir -p "$(dirname "$OUT")"
rm -f "$OUT"
UEXEC="$(cd "$(dirname "$0")/../../unity-editor-ops/scripts" && pwd)/uexec.sh"
"$UEXEC" "var p = \"$OUT\"; UnityEngine.ScreenCapture.CaptureScreenshot(p); UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); return p;" >/dev/null
for i in $(seq 1 20); do [ -f "$OUT" ] && break; sleep 0.5; done
if [ -f "$OUT" ]; then echo "CAPTURED $OUT $(file -b "$OUT")"; else echo "CAPTURE_FAILED"; exit 1; fi
