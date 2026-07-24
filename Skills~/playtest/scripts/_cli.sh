#!/usr/bin/env bash
# playtest transport — drives the RUNNING Unity Editor via the official Unity CLI
# (`unity command`, backed by the com.unity.pipeline package).
# Self-contained: no cross-skill dependency.
#
# Source from other playtest scripts:
#   . "$(cd "$(dirname "$0")" && pwd)/_cli.sh"
#
# Provides: UNITY_BIN, UCLI_ROOT, UCLI_PP[], and functions ev / ucmd / estate.
# Requires: `unity` CLI on ~/.unity/bin (or UNITY_BIN), jq, a running Editor with
# the Pipeline package (unity pipeline install) — check with `unity status`.

UNITY_BIN="${UNITY_BIN:-$HOME/.unity/bin/unity}"
export UNITY_NO_BANNER=1

# Project root = nearest ancestor of this script holding ProjectSettings/ProjectVersion.txt
_ucli_root() {
  local d; d="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
  while [ -n "$d" ] && [ "$d" != "/" ]; do
    [ -f "$d/ProjectSettings/ProjectVersion.txt" ] && { echo "$d"; return 0; }
    d="$(dirname "$d")"
  done
  return 1
}
UCLI_ROOT="${UCLI_ROOT:-$(_ucli_root)}"
UCLI_PP=(--project-path "$UCLI_ROOT")

# ucmd <command> [args...] — run a Pipeline command, print its raw --format json envelope.
ucmd() { local c="$1"; shift; "$UNITY_BIN" command "$c" "${UCLI_PP[@]}" --format json "$@" 2>/dev/null || true; }

# ev "<C# code>" — evaluate C# in the live Editor (statements + `return expr;`).
# Prints the C# return value (string unquoted / object as JSON), or "ERR:<diag>" on
# compile/run failure.
ev() {
  local out ok
  out="$("$UNITY_BIN" command eval "${UCLI_PP[@]}" --format json "$1" 2>/dev/null)" || true
  ok="$(printf '%s' "$out" | jq -r '.data.result.success // false' 2>/dev/null || echo false)"
  if [ "$ok" = "true" ]; then
    printf '%s' "$out" | jq -r '.data.result.result // empty' 2>/dev/null || true
  else
    printf 'ERR:%s\n' "$(printf '%s' "$out" | jq -r '((.data.result.diagnostics // [])|map(.id+" "+.message)|join("; ")) as $d | if ($d|length)>0 then $d else ((.errors//[])|tojson) end' 2>/dev/null || echo parse_failed)"
  fi
  return 0
}

# estate — editor play state: playing | paused | stopped | unknown (transient during reload).
estate() { ucmd editor_status | jq -r '.data.result.playMode // "unknown"' 2>/dev/null || echo unknown; }
