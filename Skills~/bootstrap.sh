#!/usr/bin/env bash
# bootstrap.sh — HWI Foundation 의 AI 스킬(공식 Unity CLI 기반)을 소비 프로젝트에 설치.
#
# 설치 대상 스킬(전부 공식 Unity CLI = `unity` 바이너리 + com.unity.pipeline 위에서 동작):
#   - unity-cli            : 에디터 구동·검증·조작(전송수단 정본, 스크립트 0)
#   - playtest             : 좌표 기반 Play Mode 테스트 + 검증 리포트
#   - unity-ai-image-gen   : Unity AI Generators 로 이미지/사운드/애니 생성
#
# 사용법:
#   bash bootstrap.sh [<unity-project-dir>] [options]
#
# Options:
#   --skip-claude-md   CLAUDE.md/AGENTS.md 안내 블록 삽입 생략
#   --force            기존 스킬 파일을 백업(.bak) 후 덮어쓰기
#   -h, --help         이 도움말 출력
#
# 이 스크립트는 자신이 위치한 Skills~/ 의 형제 스킬 디렉터리를 소스로 쓴다(별도 fetch 없음).
# unity-exec 시절의 임베드 HTTP 서버·토큰·포트는 없다 — 전송은 공식 CLI 가 담당한다.
#
# Exit code: 0=success, 1=user error, 3=copy error

set -euo pipefail

SKILLS=(unity-cli playtest unity-ai-image-gen)

PROJECT_DIR=""
SKIP_CLAUDE_MD=false
FORCE=false

log()  { printf '\033[1;34m[hwi-foundation]\033[0m %s\n' "$*" >&2; }
warn() { printf '\033[1;33m[hwi-foundation]\033[0m %s\n' "$*" >&2; }
err()  { printf '\033[1;31m[hwi-foundation]\033[0m %s\n' "$*" >&2; }

usage() { grep -E '^# ' "$0" | sed -E 's/^# ?//'; exit 0; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-claude-md) SKIP_CLAUDE_MD=true; shift ;;
    --force)          FORCE=true; shift ;;
    -h|--help)        usage ;;
    -*)               err "unknown option: $1"; exit 1 ;;
    *)                PROJECT_DIR="$1"; shift ;;
  esac
done

PROJECT_DIR="${PROJECT_DIR:-$PWD}"
PROJECT_DIR="$(cd "$PROJECT_DIR" && pwd -P)"

if [[ ! -d "$PROJECT_DIR/Assets" || ! -d "$PROJECT_DIR/Packages" ]]; then
  err "not a Unity project (missing Assets/ or Packages/): $PROJECT_DIR"
  exit 1
fi

SRC_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"   # .../Skills~
log "target project: $PROJECT_DIR"
log "skill source:   $SRC_DIR"

copy_with_backup() {
  local src="$1" dst="$2"
  if [[ -f "$dst" ]] && ! cmp -s "$src" "$dst"; then
    if [[ "$FORCE" == "true" ]]; then cp "$dst" "$dst.bak"; warn "backup: $dst → $dst.bak"
    else warn "exists (kept, use --force to overwrite): $dst"; return; fi
  fi
  cp "$src" "$dst"
}

# --- 1. install skills -------------------------------------------------------
for skill in "${SKILLS[@]}"; do
  [[ -d "$SRC_DIR/$skill" ]] || { err "skill source missing: $SRC_DIR/$skill"; exit 3; }
  DEST="$PROJECT_DIR/.claude/skills/$skill"
  # 서브트리(scripts/ references/ assets/)를 상대경로 보존하며 복사
  while IFS= read -r -d '' f; do
    rel="${f#"$SRC_DIR/$skill/"}"
    mkdir -p "$DEST/$(dirname "$rel")"
    copy_with_backup "$f" "$DEST/$rel"
  done < <(find "$SRC_DIR/$skill" -type f -print0)
  find "$DEST" -name '*.sh' -exec chmod +x {} + 2>/dev/null || true
  log "installed skill → $DEST"
done

# --- 2. CLAUDE.md / AGENTS.md guide block (idempotent) -----------------------
START_MARK="<!-- hwi-unity-cli-skill:start -->"
END_MARK="<!-- hwi-unity-cli-skill:end -->"

GUIDE_BLOCK="$START_MARK
## Unity 에디터 조작 — 공식 Unity CLI

이 프로젝트는 Unity 에디터 조작·검증·AI 생성을 **공식 Unity CLI**(\`unity\` 바이너리 + \`com.unity.pipeline\` 패키지)로 한다.
스킬은 \`.claude/skills/\` 에 설치되어 있다:

- \`unity-cli\` — 에디터 구동·검증·조작(전송수단 정본). 사용법·검증 게이트·안전 수칙.
- \`playtest\` — 좌표 기반 Play Mode 테스트 + 검증 리포트.
- \`unity-ai-image-gen\` — Unity AI Generators 로 이미지/사운드/애니 생성.

핵심 커맨드:
- 상태: \`unity status --format json\` (에디터+Pipeline 서버가 떠 있어야 함)
- C# 실행: \`unity command eval --project-path . --format json 'return UnityEngine.Application.unityVersion;'\`
- 컴파일 검증(.cs 수정 후 필수): \`unity command recompile --project-path .\` → \`unity command recompile_status --project-path . --format json\`
- 상세 규칙: \`.claude/skills/unity-cli/SKILL.md\`
$END_MARK"

insert_guide() {
  local md="$1"
  [[ "$SKIP_CLAUDE_MD" == "true" ]] && return
  if [[ ! -f "$md" ]]; then
    log "creating $md with guide block"
    printf '# %s\n\n%s\n' "$(basename "$PROJECT_DIR")" "$GUIDE_BLOCK" > "$md"; return
  fi
  if grep -qF "$START_MARK" "$md"; then
    cp "$md" "$md.bak"
    local s e
    s=$(grep -nF "$START_MARK" "$md.bak" | head -1 | cut -d: -f1)
    e=$(grep -nF "$END_MARK"   "$md.bak" | head -1 | cut -d: -f1)
    if [[ -z "$s" || -z "$e" || "$e" -le "$s" ]]; then warn "malformed guide markers in $(basename "$md") — leaving as-is"; return; fi
    { [[ "$s" -gt 1 ]] && head -n $((s - 1)) "$md.bak"; printf '%s\n' "$GUIDE_BLOCK"; tail -n +$((e + 1)) "$md.bak"; } > "$md"
    log "updated guide block in $(basename "$md")"
  else
    printf '\n%s\n' "$GUIDE_BLOCK" >> "$md"; log "appended guide block to $(basename "$md")"
  fi
}

insert_guide "$PROJECT_DIR/CLAUDE.md"
[[ -f "$PROJECT_DIR/AGENTS.md" ]] && insert_guide "$PROJECT_DIR/AGENTS.md"

# --- 3. next steps -----------------------------------------------------------
cat <<'EOF' >&2

────────────────────────────────────────
✅ HWI Foundation AI 스킬 설치 완료.

공식 Unity CLI 는 머신 단위 1회 설정이 필요하다(프로젝트마다 반복 X):
  1. CLI 설치:   curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
  2. 로그인:     unity auth login          # 브라우저 플로우 (사람 1회)
  3. 라이선스:   unity license             # 없으면 unity license activate
  4. Pipeline:   프로젝트 열고  unity pipeline install   (com.unity.pipeline, experimental)
  5. 확인:       unity status --format json

⚠ com.unity.pipeline · unity CLI 는 Unity 베타/experimental — 명령 표면이 바뀔 수 있다.
스킬 사용법은 .claude/skills/unity-cli/SKILL.md 참고.
────────────────────────────────────────
EOF
