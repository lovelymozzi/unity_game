#!/usr/bin/env bash
# playtest: annotate.py 래퍼 — 스크린샷에 빨간 표기. 인자는 annotate.py와 동일.
# 예: annotate.sh shots/01-before.png shots/01-after.png --tap 375 950 --label "01 발사"
set -euo pipefail
exec python3 "$(dirname "$0")/annotate.py" "$@"
