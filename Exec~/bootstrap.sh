#!/usr/bin/env bash
# bootstrap.sh — unity-exec 패키지 + Claude/Codex 스킬 자동 설치
#
# 사용법:
#   bash bootstrap.sh [<unity-project-dir>] [options]
#
# Options:
#   --git-url <url>     패키지 git URL (manifest.json 등록용)
#   --git-ref <ref>     git branch/tag (기본: main)
#   --source <dir>      스킬 소스 디렉터리(ClaudeSkill~ 내용물의 부모)
#   --skip-manifest     Packages/manifest.json 수정 생략
#   --skip-claude-md    CLAUDE.md/AGENTS.md 안내 블록 삽입 생략
#   --force             기존 파일을 백업 후 덮어쓰기
#   -h, --help          이 도움말 출력
#
# 환경 변수로도 동일 옵션 지정 가능:
#   UNITY_EXEC_GIT_URL, UNITY_EXEC_GIT_REF, UNITY_EXEC_SKILL_SOURCE
#
# AI 에이전트 사용 예 (빈 Unity 프로젝트에서):
#   curl -fsSL https://git.linecorp.com/LINEStudio-Client/unity-exec-cli/raw/main/bootstrap.sh \
#     | bash -s -- "$PWD" --git-url https://git.linecorp.com/LINEStudio-Client/unity-exec-cli.git
#
# Exit code: 0=success, 1=user error, 2=fetch error, 3=copy error

set -euo pipefail

PACKAGE_NAME="com.linestudio.unity-exec"
SKILL_NAME="unity-editor-ops"

PROJECT_DIR=""
GIT_URL="${UNITY_EXEC_GIT_URL:-}"
GIT_REF="${UNITY_EXEC_GIT_REF:-main}"
SKILL_SOURCE="${UNITY_EXEC_SKILL_SOURCE:-}"
SKIP_MANIFEST=false
SKIP_CLAUDE_MD=false
FORCE=false

log()  { printf '\033[1;34m[unity-exec]\033[0m %s\n' "$*" >&2; }
warn() { printf '\033[1;33m[unity-exec]\033[0m %s\n' "$*" >&2; }
err()  { printf '\033[1;31m[unity-exec]\033[0m %s\n' "$*" >&2; }

usage() {
  grep -E '^# ' "$0" | sed -E 's/^# ?//'
  exit 0
}

# --- args ---
while [[ $# -gt 0 ]]; do
  case "$1" in
    --git-url)      GIT_URL="$2"; shift 2 ;;
    --git-ref)      GIT_REF="$2"; shift 2 ;;
    --source)       SKILL_SOURCE="$2"; shift 2 ;;
    --skip-manifest) SKIP_MANIFEST=true; shift ;;
    --skip-claude-md) SKIP_CLAUDE_MD=true; shift ;;
    --force)        FORCE=true; shift ;;
    -h|--help)      usage ;;
    -*)             err "unknown option: $1"; exit 1 ;;
    *)              PROJECT_DIR="$1"; shift ;;
  esac
done

PROJECT_DIR="${PROJECT_DIR:-$PWD}"
PROJECT_DIR="$(cd "$PROJECT_DIR" && pwd -P)"

if [[ ! -d "$PROJECT_DIR/Assets" || ! -d "$PROJECT_DIR/Packages" ]]; then
  err "not a Unity project (missing Assets/ or Packages/): $PROJECT_DIR"
  exit 1
fi

log "target project: $PROJECT_DIR"

# --- resolve skill source ---
# Priority:
#   1) --source <dir>
#   2) script directory (when invoked from inside package: ClaudeSkill~/ sibling)
#   3) $PROJECT_DIR/Library/PackageCache/$PACKAGE_NAME*/ClaudeSkill~
#   4) git fetch into temp dir

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
TMP_FETCH_DIR=""
cleanup() {
  if [[ -n "$TMP_FETCH_DIR" && -d "$TMP_FETCH_DIR" ]]; then
    rm -rf "$TMP_FETCH_DIR"
  fi
  return 0
}
trap cleanup EXIT

resolve_source() {
  if [[ -n "$SKILL_SOURCE" ]]; then
    [[ -d "$SKILL_SOURCE" ]] || { err "--source not a dir: $SKILL_SOURCE"; exit 1; }
    echo "$SKILL_SOURCE"; return
  fi
  if [[ -d "$SCRIPT_DIR/ClaudeSkill~" ]]; then
    echo "$SCRIPT_DIR/ClaudeSkill~"; return
  fi
  local cache_glob="$PROJECT_DIR/Library/PackageCache/${PACKAGE_NAME}@"*
  for cand in $cache_glob; do
    if [[ -d "$cand/ClaudeSkill~" ]]; then
      echo "$cand/ClaudeSkill~"; return
    fi
  done
  if [[ -z "$GIT_URL" ]]; then
    err "no local skill source found and --git-url not set"
    err "either run from inside the package, open Unity once to populate PackageCache, or pass --git-url"
    exit 1
  fi
  log "fetching skill source from $GIT_URL@$GIT_REF"
  TMP_FETCH_DIR="$(mktemp -d -t unity-exec-bootstrap.XXXXXX)"
  git clone --quiet --depth 1 --branch "$GIT_REF" "$GIT_URL" "$TMP_FETCH_DIR/repo" >/dev/null 2>&1 || {
    err "git clone failed: $GIT_URL@$GIT_REF"; exit 2;
  }
  # ClaudeSkill~ is at repo root OR at Assets/modules/unity-exec/ClaudeSkill~ (legacy bb3-client layout)
  if   [[ -d "$TMP_FETCH_DIR/repo/ClaudeSkill~" ]]; then
    echo "$TMP_FETCH_DIR/repo/ClaudeSkill~"
  elif [[ -d "$TMP_FETCH_DIR/repo/Assets/modules/unity-exec/ClaudeSkill~" ]]; then
    echo "$TMP_FETCH_DIR/repo/Assets/modules/unity-exec/ClaudeSkill~"
  else
    err "ClaudeSkill~ not found in fetched repo"; exit 2
  fi
}

SRC="$(resolve_source)"
log "skill source: $SRC"

# Detect package.json next to source (for version stamp)
PKG_JSON=""
for cand in "$SRC/.." "$SRC/../.." ; do
  if [[ -f "$cand/package.json" ]]; then PKG_JSON="$(cd "$cand" && pwd -P)/package.json"; break; fi
done

PKG_VERSION="unknown"
if [[ -n "$PKG_JSON" ]] && command -v jq >/dev/null 2>&1; then
  PKG_VERSION="$(jq -r '.version // "unknown"' "$PKG_JSON" 2>/dev/null || echo unknown)"
fi

# --- 1. install skill files ---
DEST="$PROJECT_DIR/.claude/skills/$SKILL_NAME"
mkdir -p "$DEST/scripts" "$DEST/references"

copy_with_backup() {
  local src="$1" dst="$2"
  if [[ -f "$dst" ]] && ! cmp -s "$src" "$dst"; then
    if [[ "$FORCE" == "true" ]]; then
      cp "$dst" "$dst.bak"
      warn "backup: $dst → $dst.bak"
    else
      warn "exists (kept, use --force to overwrite): $dst"
      return
    fi
  fi
  cp "$src" "$dst"
}

copy_with_backup "$SRC/SKILL.md" "$DEST/SKILL.md"
for f in "$SRC/scripts/"*.sh; do
  [[ -f "$f" ]] || continue
  copy_with_backup "$f" "$DEST/scripts/$(basename "$f")"
done
for f in "$SRC/references/"*.md; do
  [[ -f "$f" ]] || continue
  copy_with_backup "$f" "$DEST/references/$(basename "$f")"
done

chmod +x "$DEST/scripts/"*.sh 2>/dev/null || true

# version marker
printf '%s\n' "$PKG_VERSION" > "$DEST/.installed-version"
log "installed skill v$PKG_VERSION → $DEST"

# --- 2. add manifest entry ---
if [[ "$SKIP_MANIFEST" == "false" ]]; then
  MANIFEST="$PROJECT_DIR/Packages/manifest.json"
  if [[ ! -f "$MANIFEST" ]]; then
    warn "no Packages/manifest.json — skipping manifest update"
  elif grep -q "\"$PACKAGE_NAME\"" "$MANIFEST"; then
    log "manifest already references $PACKAGE_NAME — leaving as-is"
  elif [[ -z "$GIT_URL" ]]; then
    warn "--git-url not set — skipping manifest update (add manually if needed)"
  elif ! command -v jq >/dev/null 2>&1; then
    warn "jq not installed — skipping manifest update (please add $PACKAGE_NAME manually)"
  else
    cp "$MANIFEST" "$MANIFEST.bak"
    PKG_REF="$GIT_URL"
    [[ "$GIT_REF" != "main" ]] && PKG_REF="$GIT_URL#$GIT_REF"
    jq --arg name "$PACKAGE_NAME" --arg ref "$PKG_REF" \
       '.dependencies[$name] = $ref' "$MANIFEST.bak" > "$MANIFEST"
    log "manifest: added $PACKAGE_NAME → $PKG_REF (backup: $MANIFEST.bak)"
  fi
fi

# --- 3. CLAUDE.md / AGENTS.md hint block ---
START_MARK="<!-- unity-exec-skill:start -->"
END_MARK="<!-- unity-exec-skill:end -->"

GUIDE_BLOCK="$START_MARK
## unity-exec — Unity Editor 원격 C# 실행

이 프로젝트는 Unity Editor 안의 C# 실행을 위해 \`unity-exec\` 패키지를 사용합니다.
관련 스킬은 \`.claude/skills/unity-editor-ops/\`에 설치되어 있습니다.

베이스 포트는 8090이지만 충돌 시 8091/8092…로 자동 분배됩니다. **포트를 외우지 말 것** — cwd → 인스턴스 매핑은 \`~/.unity-exec/instances.json\` + \`resolve-port.sh\`가 담당합니다.

- 포트 해석: \`.claude/skills/unity-editor-ops/scripts/resolve-port.sh\` (cwd에 매칭되는 살아있는 인스턴스의 포트를 stdout으로 반환)
- 서버 상태: \`PORT=\$(.claude/skills/unity-editor-ops/scripts/resolve-port.sh) && curl -s \"http://127.0.0.1:\$PORT/status\"\` (Unity Editor가 열려 있어야 함)
- 토큰: \`cat ~/.unity-exec/auth-token\`
- C# 실행: \`.claude/skills/unity-editor-ops/scripts/uexec.sh '<code>'\` (내부에서 \`resolve-port.sh\` 호출)
- 컴파일 검증: \`.claude/skills/unity-editor-ops/scripts/ucompile.sh\` (.cs 수정 후 필수, 내부에서 \`resolve-port.sh\` 호출)
- 상세 규칙: \`.claude/skills/unity-editor-ops/SKILL.md\`
$END_MARK"

insert_guide() {
  local md="$1"
  if [[ "$SKIP_CLAUDE_MD" == "true" ]]; then return; fi
  if [[ ! -f "$md" ]]; then
    log "creating $md with guide block"
    printf '# %s\n\n%s\n' "$(basename "$PROJECT_DIR")" "$GUIDE_BLOCK" > "$md"
    return
  fi
  if grep -qF "$START_MARK" "$md"; then
    cp "$md" "$md.bak"
    local start_line end_line
    start_line=$(grep -nF "$START_MARK" "$md.bak" | head -1 | cut -d: -f1)
    end_line=$(grep -nF "$END_MARK" "$md.bak" | head -1 | cut -d: -f1)
    if [[ -z "$start_line" || -z "$end_line" || "$end_line" -le "$start_line" ]]; then
      warn "malformed guide markers in $(basename "$md") — leaving as-is"
      return
    fi
    {
      [[ "$start_line" -gt 1 ]] && head -n $((start_line - 1)) "$md.bak"
      printf '%s\n' "$GUIDE_BLOCK"
      tail -n +$((end_line + 1)) "$md.bak"
    } > "$md"
    log "updated guide block in $(basename "$md") (backup: $(basename "$md").bak)"
  else
    printf '\n%s\n' "$GUIDE_BLOCK" >> "$md"
    log "appended guide block to $(basename "$md")"
  fi
}

insert_guide "$PROJECT_DIR/CLAUDE.md"
if [[ -f "$PROJECT_DIR/AGENTS.md" ]]; then
  insert_guide "$PROJECT_DIR/AGENTS.md"
fi

# --- 4. final instructions ---
cat <<'EOF' >&2

────────────────────────────────────────
✅ unity-exec bootstrap done.

Next steps:
  1. Open the Unity project. The unity-exec server starts automatically
     (base port 8090, falls back to 8091/8092... if already in use —
      see ~/.unity-exec/instances.json for the actual port).
  2. Set Unity > Settings... > General > Interaction Mode = "No Throttling".
     (Windows/Linux: Edit > Settings...)
  3. Verify (do not hardcode the port — use resolve-port.sh):
       PORT=$(.claude/skills/unity-editor-ops/scripts/resolve-port.sh)
       curl -s "http://127.0.0.1:$PORT/status"
  4. Use the skill via Claude Code / Codex CLI as usual.

If the AI assistant asks how to run code in Unity, point it at:
  .claude/skills/unity-editor-ops/SKILL.md
────────────────────────────────────────
EOF
