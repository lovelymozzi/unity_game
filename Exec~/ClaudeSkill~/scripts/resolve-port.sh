#!/usr/bin/env bash
# resolve-port.sh — 현재 cwd에 매칭되는 Unity 인스턴스의 포트를 stdout으로 출력.
#
# 규칙:
#   1) ~/.unity-exec/instances.json 의 각 항목 중 pid 가 살아있는 것만 후보
#   2) 후보의 project(=<projectRoot>/Assets)의 부모 디렉터리가 $(pwd -P) 의 prefix
#      이면 매치. 다중 매치 시 가장 deep 한(긴 경로) 항목 선택.
#   3) 매치 없으면 stderr 에 안내 후 exit 1.
#
# 환경변수 의존 없음. 호출자는 BASE_URL="http://127.0.0.1:$(resolve-port.sh)" 처럼 사용.
set -euo pipefail

INSTANCES_FILE="$HOME/.unity-exec/instances.json"
CWD="$(pwd -P)"

if [[ ! -f "$INSTANCES_FILE" ]]; then
  echo "resolve-port: no instances file at $INSTANCES_FILE" >&2
  echo "→ open Unity for $CWD first" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "resolve-port: jq required but not installed" >&2
  exit 1
fi

BEST_PORT=""
BEST_LEN=-1

# 인스턴스 목록을 [port, projectRoot, pid] TSV 라인으로 펼침. bash 3.2 호환 위해 while-read 사용.
while IFS=$'\t' read -r port proj_root pid; do
  [[ -z "$port" || -z "$proj_root" || -z "$pid" ]] && continue

  # pid 살아있나
  if ! kill -0 "$pid" 2>/dev/null; then
    continue
  fi

  # canonical 비교 (디렉터리 없으면 stale 엔트리로 스킵)
  if [[ ! -d "$proj_root" ]]; then continue; fi
  proj_canon="$(cd "$proj_root" 2>/dev/null && pwd -P || echo "$proj_root")"

  # cwd == proj_canon 또는 cwd 가 proj_canon 하위
  if [[ "$CWD" == "$proj_canon" || "$CWD" == "$proj_canon"/* ]]; then
    len=${#proj_canon}
    if (( len > BEST_LEN )); then
      BEST_LEN=$len
      BEST_PORT=$port
    fi
  fi
done < <(jq -r '.[] | [.port, (.project | rtrimstr("/Assets") | rtrimstr("/")), .pid] | @tsv' "$INSTANCES_FILE")

if [[ -z "$BEST_PORT" ]]; then
  echo "resolve-port: no live Unity instance matches cwd $CWD" >&2
  echo "→ open Unity for this project, or cd into a project whose Editor is running" >&2
  echo "→ live instances:" >&2
  jq -r '.[] | "    pid=\(.pid) port=\(.port) project=\(.project)"' "$INSTANCES_FILE" >&2 || true
  exit 1
fi

echo "$BEST_PORT"
