#!/usr/bin/env bash
# ucompile.sh — 스크립트 컴파일 트리거 + 완료 대기 + 결과 반환
#
# Usage: ucompile.sh [options]
#   --timeout SEC    최대 대기 시간 (기본: 120초)
#   --poll SEC       폴링 간격 (기본: 2초)
#   --full           전체 리컴파일 강제 (기본: 변경 감지 후 필요시만)
#   --no-trigger     트리거 없이 현재 컴파일 상태만 폴링
#
# Exit code: 0=success, 1=failed, 2=timeout, 3=server error
set -euo pipefail

TIMEOUT_SEC=120
POLL_SEC=2
FULL=false
NO_TRIGGER=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --timeout) TIMEOUT_SEC="$2"; shift 2 ;;
    --poll) POLL_SEC="$2"; shift 2 ;;
    --full) FULL=true; shift ;;
    --no-trigger) NO_TRIGGER=true; shift ;;
    *) shift ;;
  esac
done

TOKEN="$(cat "$HOME/.unity-exec/auth-token")"
PORT="$("$(dirname "$0")/resolve-port.sh")" || exit 1
BASE_URL="http://127.0.0.1:$PORT"

# jq 헬퍼: boolean false를 올바르게 추출 (.field // default 는 false에도 fallback 적용됨)
jq_bool() { jq -r "if $1 == null then \"error\" else ($1 | tostring) end"; }

# 0. 서버 상태 확인
STATUS=$(curl -s --max-time 5 "$BASE_URL/status" 2>/dev/null) || {
  echo '{"error":"unity-exec server not responding"}' >&2
  exit 3
}

# 1. 현재 컴파일 상태 확인
if [[ "$NO_TRIGGER" == "true" ]]; then
  echo "⏳ Polling current compile status (timeout: ${TIMEOUT_SEC}s)..." >&2
else
  CURRENT=$(curl -s --max-time 5 "$BASE_URL/compile" -H "X-Auth-Token: $TOKEN" 2>/dev/null) || true
  CURRENT_COMPILING=$(echo "$CURRENT" | jq_bool '.data.isCompiling' 2>/dev/null) || true

  if [[ "$CURRENT_COMPILING" == "true" ]]; then
    echo "⏳ Compilation already in progress. Waiting..." >&2
    NO_TRIGGER=true
  else
    BODY='{}'
    [[ "$FULL" == "true" ]] && BODY='{"full":true}'

    curl -s --max-time 10 -X POST "$BASE_URL/compile" \
      -H "X-Auth-Token: $TOKEN" \
      -H "Content-Type: application/json" \
      -d "$BODY" >/dev/null 2>&1 || {
      echo '{"error":"Failed to trigger compilation"}' >&2
      exit 3
    }

    MODE="AssetDatabase.Refresh"
    [[ "$FULL" == "true" ]] && MODE="Full recompilation"
    echo "⏳ $MODE triggered. Polling for result (timeout: ${TIMEOUT_SEC}s)..." >&2

    # Refresh 직후 짧은 대기 — Unity가 변경 감지 후 컴파일 시작할 시간
    sleep 1
  fi
fi

# 2. 폴링 — isCompiling=false && lastResult != "unknown" 될 때까지 대기
ELAPSED=0
SERVER_DOWN=false

while (( ELAPSED < TIMEOUT_SEC )); do
  sleep "$POLL_SEC"
  ELAPSED=$((ELAPSED + POLL_SEC))

  RESULT=$(curl -s --max-time 5 "$BASE_URL/compile" \
    -H "X-Auth-Token: $TOKEN" 2>/dev/null) || {
    if [[ "$SERVER_DOWN" == "false" ]]; then
      echo "  🔄 Domain reload in progress..." >&2
      SERVER_DOWN=true
    fi
    continue
  }

  if [[ "$SERVER_DOWN" == "true" ]]; then
    echo "  ✓ Server back online" >&2
    SERVER_DOWN=false
  fi

  IS_COMPILING=$(echo "$RESULT" | jq_bool '.data.isCompiling')
  LAST_RESULT=$(echo "$RESULT" | jq -r '.data.lastResult // "unknown"')

  if [[ "$IS_COMPILING" == "false" && "$LAST_RESULT" != "unknown" ]]; then
    echo "$RESULT" | jq '.data'
    if [[ "$LAST_RESULT" == "success" ]]; then
      echo "✅ Compilation succeeded." >&2
      exit 0
    else
      echo "❌ Compilation failed." >&2
      exit 1
    fi
  fi

  echo "  ... compiling (${ELAPSED}s/${TIMEOUT_SEC}s)" >&2
done

# 3. 타임아웃 — 마지막으로 한 번 더 체크
FINAL=$(curl -s --max-time 5 "$BASE_URL/compile" -H "X-Auth-Token: $TOKEN" 2>/dev/null) || true
FINAL_COMPILING=$(echo "$FINAL" | jq_bool '.data.isCompiling' 2>/dev/null) || true
FINAL_RESULT=$(echo "$FINAL" | jq -r '.data.lastResult // "unknown"' 2>/dev/null) || true

if [[ "$FINAL_COMPILING" == "false" && "$FINAL_RESULT" != "unknown" ]]; then
  echo "$FINAL" | jq '.data'
  if [[ "$FINAL_RESULT" == "success" ]]; then
    echo "✅ Compilation succeeded (caught at timeout boundary)." >&2
    exit 0
  else
    echo "❌ Compilation failed." >&2
    exit 1
  fi
fi

echo '{"error":"Compilation timed out","timeout_sec":'"$TIMEOUT_SEC"'}' | jq .
echo "⏰ Compilation timed out after ${TIMEOUT_SEC}s." >&2
exit 2
