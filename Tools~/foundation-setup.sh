#!/usr/bin/env bash
# foundation-setup.sh — HWI Foundation 신규 프로젝트 셋업 (한 번에)
#
#   1) Templates~/ProjectConventions.md 내용을 <project>/CLAUDE.md 의
#      <!-- hwi-foundation:start/end --> 마커 블록에 멱등 삽입/교체.
#   2) Skills~/bootstrap.sh 로 공식 Unity CLI 기반 AI 스킬(unity-cli·playtest·unity-ai-image-gen)
#      설치 + <!-- hwi-unity-cli-skill:* --> 안내 블록 주입.
#
# 두 마커 블록은 서로 독립 → 재실행/업스트림 재동기화 안전.
#
# 사용:
#   bash Packages/com.hwi.foundation/Tools~/foundation-setup.sh [<project-dir>] [--skip-skills] [--skip-conventions]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PKG_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

PROJECT_DIR="${1:-$PWD}"
case "${1:-}" in --*) PROJECT_DIR="$PWD" ;; esac
SKIP_CONVENTIONS=false
SKIP_SKILLS=false
for a in "$@"; do
  case "$a" in
    --skip-conventions) SKIP_CONVENTIONS=true ;;
    --skip-skills) SKIP_SKILLS=true ;;
  esac
done

log()  { printf '\033[1;34m[hwi-foundation]\033[0m %s\n' "$1"; }
err()  { printf '\033[1;31m[hwi-foundation]\033[0m %s\n' "$1" >&2; }

[[ -d "$PROJECT_DIR" ]] || { err "project dir not found: $PROJECT_DIR"; exit 1; }
PROJECT_DIR="$(cd "$PROJECT_DIR" && pwd)"

START_MARK="<!-- hwi-foundation:start -->"
END_MARK="<!-- hwi-foundation:end -->"

inject_conventions() {
  local src="$PKG_DIR/Templates~/ProjectConventions.md"
  local md="$PROJECT_DIR/CLAUDE.md"
  [[ -f "$src" ]] || { err "ProjectConventions.md not found: $src"; exit 2; }

  # HTML 주석 헤더를 벗기고 첫 헤딩(# )부터 본문만 추출
  local body
  body="$(awk 'f{print} /^# /{if(!f){f=1; print}}' "$src")"
  local block
  block="$START_MARK
$body
$END_MARK"

  if [[ ! -f "$md" ]]; then
    printf '# %s\n\n%s\n' "$(basename "$PROJECT_DIR")" "$block" > "$md"
    log "created CLAUDE.md with foundation block"
    return
  fi
  if grep -qF "$START_MARK" "$md"; then
    cp "$md" "$md.bak"
    local s e
    s=$(grep -nF "$START_MARK" "$md.bak" | head -1 | cut -d: -f1)
    e=$(grep -nF "$END_MARK"   "$md.bak" | head -1 | cut -d: -f1)
    if [[ -z "$s" || -z "$e" || "$e" -le "$s" ]]; then
      err "malformed foundation markers in CLAUDE.md — leaving as-is"; return
    fi
    {
      [[ "$s" -gt 1 ]] && head -n $((s - 1)) "$md.bak"
      printf '%s\n' "$block"
      tail -n +$((e + 1)) "$md.bak"
    } > "$md"
    log "updated foundation block in CLAUDE.md (backup: CLAUDE.md.bak)"
  else
    printf '\n%s\n' "$block" >> "$md"
    log "appended foundation block to CLAUDE.md"
  fi
}

if [[ "$SKIP_CONVENTIONS" == "false" ]]; then
  inject_conventions
else
  log "skip conventions (--skip-conventions)"
fi

# --- AI 스킬 설치 (공식 Unity CLI 기반) — Skills~/bootstrap.sh 에 위임 ---
if [[ "$SKIP_SKILLS" == "false" ]]; then
  if [[ -f "$PKG_DIR/Skills~/bootstrap.sh" ]]; then
    log "installing Unity CLI AI skills (Skills~/bootstrap.sh)"
    bash "$PKG_DIR/Skills~/bootstrap.sh" "$PROJECT_DIR"
  else
    err "Skills~/bootstrap.sh not found — AI 스킬 설치 생략"
  fi
else
  log "skip skills (--skip-skills)"
fi

log "done. CLAUDE.md = [hwi-foundation 블록] + [hwi-unity-cli-skill 블록]. skills = unity-cli + playtest + unity-ai-image-gen."
