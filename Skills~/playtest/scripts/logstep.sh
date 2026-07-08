#!/usr/bin/env bash
# playtest: progress.md 에 스텝 항목을 일관된 형식으로 append.
# 사용: logstep.sh <RUNDIR> <스텝번호> "<액션>" "<관찰>" "<프로브 JSON 또는 메모>" ["<after 스샷 상대경로>"]
set -euo pipefail
RUNDIR="${1:?usage: logstep.sh <RUNDIR> <n> <action> <observe> <probe> [shot]}"
N="$2"; ACTION="$3"; OBSERVE="${4:-}"; PROBE="${5:-}"; SHOT="${6:-}"
F="$RUNDIR/progress.md"
{
  echo ""
  echo "## Step ${N}"
  echo "- 액션: ${ACTION}"
  [ -n "$OBSERVE" ] && echo "- 관찰: ${OBSERVE}"
  [ -n "$SHOT" ] && echo "- 스샷: ![step${N}](${SHOT})"
  if [ -n "$PROBE" ]; then
    echo "- 프로브:"
    echo '```json'
    echo "$PROBE"
    echo '```'
  fi
} >> "$F"
echo "LOGGED step ${N} -> $F"
