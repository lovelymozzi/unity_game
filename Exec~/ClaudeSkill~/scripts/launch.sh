#!/usr/bin/env bash
# launch.sh — 프로젝트의 Unity Editor를 콜드 스타트로 띄우고 unity-exec 서버가 ready 될 때까지 대기.
#
# 사용법:
#   launch.sh [<unity-project-dir>] [--timeout <sec>] [--poll <sec>]
#   - 프로젝트 경로 생략 시 cwd 에서 위로 올라가며 ProjectSettings/ProjectVersion.txt 를 탐색.
#
# 동작:
#   1) 이미 해당 프로젝트의 unity-exec 인스턴스가 살아있으면 재실행하지 않고 즉시 ready 보고 후 종료.
#   2) ProjectSettings/ProjectVersion.txt 에서 에디터 버전(m_EditorVersion) 파싱.
#   3) Unity Hub 설치 경로에서 해당 버전 Unity.app 탐색 (UNITY_EDITOR_ROOT 로 루트 override 가능).
#   4) open -a 로 -projectPath 지정해 실행 (즉시 반환).
#   5) ~/.unity-exec/instances.json 에 이 프로젝트 엔트리가 등장하고 /status state=ready 될 때까지 폴링.
#
# Exit code: 0=ready, 1=user error(경로/버전/에디터 없음), 2=timeout
#
# macOS 전용 (open -a 사용). jq 필요.
set -euo pipefail

UNITY_EDITOR_ROOT="${UNITY_EDITOR_ROOT:-/Applications/Unity/Hub/Editor}"
INSTANCES_FILE="$HOME/.unity-exec/instances.json"
TIMEOUT=300
POLL=3
PROJECT_ARG=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --timeout) TIMEOUT="$2"; shift 2 ;;
    --poll)    POLL="$2"; shift 2 ;;
    -*)        echo "launch: unknown option: $1" >&2; exit 1 ;;
    *)         PROJECT_ARG="$1"; shift ;;
  esac
done

command -v jq >/dev/null 2>&1 || { echo "launch: jq required but not installed" >&2; exit 1; }

# --- 프로젝트 루트 탐색 (ProjectSettings/ProjectVersion.txt 보유 디렉터리) ---
find_project_root() {
  local dir; dir="$(cd "${1:-$PWD}" 2>/dev/null && pwd -P)" || return 1
  while [[ -n "$dir" && "$dir" != "/" ]]; do
    [[ -f "$dir/ProjectSettings/ProjectVersion.txt" ]] && { echo "$dir"; return 0; }
    dir="$(dirname "$dir")"
  done
  [[ -f "/ProjectSettings/ProjectVersion.txt" ]] && { echo "/"; return 0; }
  return 1
}

PROJ="$(find_project_root "$PROJECT_ARG")" || {
  echo "launch: ProjectVersion.txt 를 찾을 수 없음 (cwd 또는 인자 경로의 상위에서 탐색 실패)" >&2
  echo "→ Unity 프로젝트 루트에서 실행하거나 경로를 인자로 전달" >&2
  exit 1
}

# --- 이미 살아있는 인스턴스가 있으면 재실행하지 않음 ---
live_port_for_project() {
  [[ -f "$INSTANCES_FILE" ]] || return 1
  local port proj_root pid
  while IFS=$'\t' read -r port proj_root pid; do
    [[ -z "$port" || -z "$proj_root" || -z "$pid" ]] && continue
    kill -0 "$pid" 2>/dev/null || continue
    [[ -d "$proj_root" ]] || continue
    local canon; canon="$(cd "$proj_root" 2>/dev/null && pwd -P || echo "$proj_root")"
    if [[ "$canon" == "$PROJ" ]]; then echo "$port"; return 0; fi
  done < <(jq -r '.[] | [.port, (.project | rtrimstr("/Assets") | rtrimstr("/")), .pid] | @tsv' "$INSTANCES_FILE")
  return 1
}

status_state() { curl -s "http://127.0.0.1:$1/status" 2>/dev/null | jq -r '.data.state // empty' 2>/dev/null; }

if PORT="$(live_port_for_project)"; then
  STATE="$(status_state "$PORT")"
  echo "launch: 이미 실행 중 — port=$PORT state=${STATE:-unknown} project=$PROJ"
  [[ "$STATE" == "ready" ]] && exit 0
  echo "launch: state=ready 대기..." >&2
fi

# --- 에디터 버전 → Unity.app 경로 ---
VER="$(grep -E '^m_EditorVersion:' "$PROJ/ProjectSettings/ProjectVersion.txt" | awk '{print $2}')"
[[ -n "$VER" ]] || { echo "launch: ProjectVersion.txt 에서 m_EditorVersion 파싱 실패" >&2; exit 1; }

APP="$UNITY_EDITOR_ROOT/$VER/Unity.app"
if [[ ! -d "$APP" ]]; then
  echo "launch: 에디터 미설치 — $APP 없음 (필요 버전: $VER)" >&2
  echo "→ Unity Hub 에서 $VER 설치, 또는 UNITY_EDITOR_ROOT 로 설치 루트 지정" >&2
  echo "→ 설치된 버전:" >&2
  ls -1 "$UNITY_EDITOR_ROOT" 2>/dev/null | sed 's/^/    /' >&2 || true
  exit 1
fi

echo "launch: Unity $VER 실행 — $PROJ" >&2
# -n: 항상 새 인스턴스로 띄움. 다른 프로젝트의 Unity 가 이미 실행 중일 때
# -a 단독은 기존 인스턴스를 활성화만 하고 -projectPath 를 무시한다.
open -na "$APP" --args -projectPath "$PROJ"

# --- ready 폴링 ---
ELAPSED=0
while (( ELAPSED < TIMEOUT )); do
  if PORT="$(live_port_for_project)"; then
    if [[ "$(status_state "$PORT")" == "ready" ]]; then
      echo "launch: ready — port=$PORT project=$PROJ (약 ${ELAPSED}s)"
      exit 0
    fi
  fi
  sleep "$POLL"
  ELAPSED=$(( ELAPSED + POLL ))
done

echo "launch: ${TIMEOUT}s 내 ready 도달 실패 (에디터는 부팅 중일 수 있음 — /status 로 재확인)" >&2
exit 2
