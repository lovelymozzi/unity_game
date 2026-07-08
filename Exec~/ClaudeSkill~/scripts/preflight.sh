#!/usr/bin/env bash
set -euo pipefail

PORT="$("$(dirname "$0")/resolve-port.sh")" || exit 1
STATUS_URL="http://127.0.0.1:$PORT/status"
TOKEN_FILE="$HOME/.unity-exec/auth-token"

status_json="$(curl -s "$STATUS_URL")"
echo "$status_json" | jq . >/dev/null 2>&1 || {
  echo "PRECHECK_FAIL: invalid /status response"
  echo "$status_json"
  exit 1
}

ok="$(echo "$status_json" | jq -r '.success // false')"
state="$(echo "$status_json" | jq -r '.data.state // "unknown"')"
port="$(echo "$status_json" | jq -r '.data.port // "unknown"')"
queue="$(echo "$status_json" | jq -r '.data.queue.pendingExecRequests // 0')"

if [[ "$ok" != "true" ]]; then
  echo "PRECHECK_FAIL: server status not successful"
  echo "$status_json"
  exit 1
fi

if [[ ! -f "$TOKEN_FILE" ]]; then
  echo "PRECHECK_FAIL: missing token file $TOKEN_FILE"
  exit 1
fi

token_len="$(wc -c < "$TOKEN_FILE" | tr -d ' ')"
if [[ "$token_len" -lt 20 ]]; then
  echo "PRECHECK_FAIL: token looks invalid (len=$token_len)"
  exit 1
fi

echo "PRECHECK_OK: port=$port state=$state pendingQueue=$queue tokenFile=$TOKEN_FILE"
if [[ "$state" == "compiling" ]]; then
  echo "PRECHECK_WARN: Unity is compiling; exec may fail or delay."
fi
