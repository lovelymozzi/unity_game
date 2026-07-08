#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: uexec.sh '<code>' [usings_csv] [timeout_sec]" >&2
  exit 1
fi

CODE="$1"
USINGS_CSV="${2:-}"
TIMEOUT_SEC="${3:-30}"
TOKEN="$(cat "$HOME/.unity-exec/auth-token")"
PORT="$("$(dirname "$0")/resolve-port.sh")" || exit 1
URL="http://127.0.0.1:$PORT/exec"

if [[ -n "$USINGS_CSV" ]]; then
  PAYLOAD="$(jq -n --arg code "$CODE" --arg us "$USINGS_CSV" '{code:$code, usings: ($us | split(",") | map(gsub("^\\s+|\\s+$"; "")) | map(select(length>0)))}')"
else
  PAYLOAD="$(jq -n --arg code "$CODE" '{code:$code}')"
fi

curl -s --max-time "$TIMEOUT_SEC" -X POST "$URL" \
  -H "X-Auth-Token: $TOKEN" \
  -H "Content-Type: application/json" \
  --data-binary "$PAYLOAD"
