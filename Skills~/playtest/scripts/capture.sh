#!/usr/bin/env bash
# playtest: 게임뷰를 PNG로 캡쳐 (edit/play 모두). 공식 Unity CLI `screenshot` 명령 사용.
set -euo pipefail
OUT="${1:-/tmp/uitest/shot.png}"
mkdir -p "$(dirname "$OUT")"
rm -f "$OUT"
. "$(cd "$(dirname "$0")" && pwd)/_cli.sh"
ucmd screenshot --view game --output "$OUT" >/dev/null 2>&1 || true
for i in $(seq 1 20); do [ -f "$OUT" ] && break; sleep 0.5; done
if [ -f "$OUT" ]; then echo "CAPTURED $OUT $(file -b "$OUT")"; else echo "CAPTURE_FAILED"; exit 1; fi
