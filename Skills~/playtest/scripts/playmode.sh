#!/usr/bin/env bash
# playtest: Play Mode 진입(on)/종료(off) + 도메인 리로드 재연결 폴링.
# 공식 Unity CLI `editor_play`/`editor_stop` + `editor_status`(playMode) 사용.
# poll: 3s 간격, 최대 40회(~125s). exit 0=원하는 state 도달, 2=timeout.
set -euo pipefail
MODE="${1:-on}"
. "$(cd "$(dirname "$0")" && pwd)/_cli.sh"
if [ "$MODE" = "on" ]; then WANT="playing"; ucmd editor_play >/dev/null 2>&1 || true; else WANT="stopped"; ucmd editor_stop >/dev/null 2>&1 || true; fi
sleep 4
for i in $(seq 1 40); do
  if [ "$(estate)" = "$WANT" ]; then echo "STATE=$WANT (i=$i)"; exit 0; fi
  sleep 3
done
echo "TIMEOUT waiting for $WANT"; exit 2
