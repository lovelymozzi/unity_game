#!/usr/bin/env bash
# unity-ai-image-gen 구동기. Unity AI(internal AssetGenerators)를 공식 Unity CLI(`unity command eval`) + AiGenProbe 리플렉션으로 구동.
# 모든 비동기 작업은 fire-and-forget + EditorPrefs("AiGenProbe.Status") 폴링으로 완료를 감지한다.
#
# 사용:
#   uai.sh ensure
#   uai.sh models [grep패턴]
#   uai.sh gen   --prompt "..." --out Assets/Generated/x.png [--model gemini-3.0-pro] [--ref Assets/.../r.png] [--remove-bg]   # 기본 모델=Nanobanana Pro(gemini-3.0-pro)
#   uai.sh sound --prompt "..." --out Assets/Generated/x.wav [--model elevenlabs-sound-effects-v2] [--duration 3] [--loop]
#   uai.sh anim  --prompt "..." --out Assets/Generated/x.anim [--model unity-text-to-motion] [--duration 3] [--video V.mp4]
#   uai.sh spritesheet --prompt "..." --sheet Assets/Generated/sheet.png [--clip Assets/Generated/x.anim] [--ref first.png] [--model video-seedance-1-pro] [--cols 4 --rows 4 --fps 12 --ppu 100 --no-key]
#       → 참조이미지→애니영상→프레임시트(+--clip 주면 키잉+슬라이스+sprite-swap 클립까지). 2D 캐릭터 모션 권장 경로.
#   uai.sh clip --sheet S.png --out x.anim [--cols 4 --rows 4 --fps 12 --ppu 100 --key --no-loop]   # 평면시트→클립(AI 0). --key=단색배경 투명키잉
#   uai.sh resize --src S.png --dst D.png --w 108 --h 108
#   uai.sh status
set -euo pipefail

SKILL_DIR="$(cd "$(dirname "$0")/.." && pwd)"            # .../.claude/skills/unity-ai-image-gen

# --- 전송: 공식 Unity CLI (`unity command`, com.unity.pipeline) ---------------
UNITY_BIN="${UNITY_BIN:-$HOME/.unity/bin/unity}"
export UNITY_NO_BANNER=1
UCLI_ROOT="${UCLI_ROOT:-$(cd "$(dirname "$0")/../../../.." && pwd)}"   # 프로젝트 루트
UCLI_PP=(--project-path "$UCLI_ROOT")

# ex "<csharp>" [timeout] — 라이브 에디터에서 C# 실행(문장+return). 반환값(문자열/JSON) 출력, 실패 시 "ERR:...".
ex() {
  local out ok
  out="$("$UNITY_BIN" command eval "${UCLI_PP[@]}" --timeout "${2:-60}" --format json "$1" 2>/dev/null)" || true
  ok="$(printf '%s' "$out" | jq -r '.data.result.success // false' 2>/dev/null || echo false)"
  if [ "$ok" = "true" ]; then printf '%s' "$out" | jq -r '.data.result.result // empty' 2>/dev/null || true
  else printf 'ERR:%s\n' "$(printf '%s' "$out" | jq -r '((.data.result.diagnostics // [])|map(.id+" "+.message)|join("; ")) as $d| if ($d|length)>0 then $d else ((.errors//[])|tojson) end' 2>/dev/null || echo parse_failed)"; fi
  return 0
}
# recompile_wait [maxPolls] — recompile 트리거 후 도메인리로드 관통 폴링(일시 에러 허용).
recompile_wait() {
  "$UNITY_BIN" command recompile "${UCLI_PP[@]}" >/dev/null 2>&1 || true
  local i rs
  for i in $(seq 1 "${1:-45}"); do
    rs="$("$UNITY_BIN" command recompile_status "${UCLI_PP[@]}" --format json 2>/dev/null | jq -r '.data.result.status // "?"' 2>/dev/null || echo '?')"
    case "$rs" in completed|up_to_date|idle) return 0;; esac
    sleep 4
  done
  return 0
}
b64() { printf '%s' "${1:-}" | base64 | tr -d '\n'; }
dec() { printf 'System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String("%s"))' "$1"; }

probe_call() {  # probe_call <Method> <new object[]{...} 인자코드>  → fire-and-forget invoke
  local method="$1" args="${2:-null}"
  ex "var t=System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new System.Type[0];}}).FirstOrDefault(x=>x.Name==\"AiGenProbe\"); if(t==null) return \"NO_PROBE\"; UnityEditor.EditorPrefs.DeleteKey(\"AiGenProbe.Status\"); t.GetMethod(\"$method\").Invoke(null, $args); return \"kicked\";"
}

get_status() { ex 'return UnityEditor.EditorPrefs.GetString("AiGenProbe.Status","<none>");'; }

poll() {  # poll <DONE_substr> <ERR_substr> <timeout_sec>
  local done="$1" err="$2" max="${3:-180}" waited=0 step=6
  while (( waited < max )); do
    sleep "$step"; waited=$((waited+step))
    local s; s="$(get_status)"
    echo "  [$waited s] $s" >&2
    if [[ "$s" == *"$done"* ]]; then echo "$s"; return 0; fi
    if [[ "$s" == *"$err"* ]];  then echo "$s"; return 1; fi
  done
  echo "TIMEOUT after ${max}s (last: $(get_status))" >&2
  return 2
}

need_editor() {
  [ "$(ex 'return "ok";')" = "ok" ] && return 0
  echo "Unity 에디터(Pipeline 서버) 미감지. 먼저 띄우세요: $UNITY_BIN open \"$UCLI_ROOT\" (또는 Unity Hub)." >&2
  exit 3
}

# --- subcommands -------------------------------------------------------------
cmd_ensure() {
  need_editor
  local loaded; loaded="$(ex 'var t=System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>{try{return a.GetTypes();}catch{return new System.Type[0];}}).FirstOrDefault(x=>x.Name=="AiGenProbe"); return t==null?"NO":"YES";')"
  if [[ "$loaded" == *YES* ]]; then echo "AiGenProbe 로드됨 (ensure OK)"; return 0; fi
  # AiGenProbe 는 com.hwi.foundation 패키지에 번들됨(Editor/AiGenProbe/) → 별도 설치 불필요.
  # 미로드면 컴파일 실패 상태다. 가장 흔한 원인:
  #   1) com.unity.2d.sprite 미설치 (AiGenProbe.asmdef 가 Unity.2D.Sprite.Editor 참조)
  #   2) 컴파일 에러(다른 스크립트) → 도메인 미완
  echo "AiGenProbe 미로드 — com.hwi.foundation 에 번들돼 있으나 컴파일 안 됨." >&2
  echo "  · com.unity.2d.sprite 설치 확인(asmdef 가 Unity.2D.Sprite.Editor 참조)" >&2
  echo "  · 컴파일 에러 해소 후 재시도: unity command recompile_status 로 상태 확인" >&2
  exit 1
}

cmd_models() {
  need_editor
  probe_call ListModels null >/dev/null
  poll "MODELS:DONE" "MODELS:ERROR" 60 >/dev/null || { echo "모델 조회 실패" >&2; exit 1; }
  local out; out="$(ex 'return UnityEditor.EditorPrefs.GetString("AiGenProbe.Models","");')"
  # ex 는 JSON 래핑 결과를 주므로 data 추출은 호출측에서. 여기선 raw 출력.
  if [[ -n "${1:-}" ]]; then echo "$out" | grep -i "$1" || true; else echo "$out"; fi
}

cmd_gen() {
  # 기본 모델 = gemini-3.0-pro (Nanobanana Pro): Nano Banana 계열 최고 품질.
  # ※ 검증결과 3.0-pro 는 불투명 플랫 배경을 낸다(flash 와 달리 투명 네이티브 아님) → 캐릭터/스프라이트 투명이 필요하면 --remove-bg.
  # removeBg 기본 false: 풀씬(키비주얼/배경)은 불투명이 정상. 투명 네이티브가 필요하거나 더 빠르게는 --model gemini-3.1-flash.
  local prompt="" out="" model="gemini-3.0-pro" ref="" removeBg="false"
  while (( $# )); do case "$1" in
    --prompt) prompt="$2"; shift 2;; --out) out="$2"; shift 2;;
    --model) model="$2"; shift 2;; --ref) ref="$2"; shift 2;;
    --no-bg-removal) removeBg="false"; shift;;
    --remove-bg) removeBg="true"; shift;;
    *) echo "unknown arg: $1" >&2; exit 2;; esac; done
  [[ -z "$prompt" || -z "$out" ]] && { echo "필수: --prompt, --out" >&2; exit 2; }
  need_editor
  echo "생성: model=$model out=$out ref=${ref:-<none>} removeBg=$removeBg (포인트 소모)"
  probe_call Kick "new object[]{ $(dec "$(b64 "$prompt")"), $(dec "$(b64 "$out")"), $(dec "$(b64 "$model")"), $(dec "$(b64 "$ref")"), $removeBg }" >/dev/null
  poll "GEN:DONE" "GEN:ERROR" 240
}

cmd_resize() {
  local src="" dst="" w="" h=""
  while (( $# )); do case "$1" in
    --src) src="$2"; shift 2;; --dst) dst="$2"; shift 2;;
    --w) w="$2"; shift 2;; --h) h="$2"; shift 2;;
    *) echo "unknown arg: $1" >&2; exit 2;; esac; done
  [[ -z "$src" || -z "$dst" || -z "$w" || -z "$h" ]] && { echo "필수: --src --dst --w --h" >&2; exit 2; }
  need_editor
  probe_call Resize "new object[]{ $(dec "$(b64 "$src")"), $(dec "$(b64 "$dst")"), $w, $h }" >/dev/null
  poll "RESIZE:DONE" "RESIZE:ERROR" 60
}

cmd_sound() {
  local prompt="" out="" model="elevenlabs-sound-effects-v2" dur="3" loop="false"
  while (( $# )); do case "$1" in
    --prompt) prompt="$2"; shift 2;; --out) out="$2"; shift 2;;
    --model) model="$2"; shift 2;; --duration) dur="$2"; shift 2;;
    --loop) loop="true"; shift;;
    *) echo "unknown arg: $1" >&2; exit 2;; esac; done
  [[ -z "$prompt" || -z "$out" ]] && { echo "필수: --prompt, --out" >&2; exit 2; }
  need_editor
  echo "사운드 생성: model=$model out=$out dur=${dur}s loop=$loop (포인트 소모)"
  probe_call KickSound "new object[]{ $(dec "$(b64 "$prompt")"), $(dec "$(b64 "$out")"), $(dec "$(b64 "$model")"), ${dur}f, $loop }" >/dev/null
  poll "GEN:DONE" "GEN:ERROR" 300
}

cmd_anim() {
  local prompt="" out="" model="unity-text-to-motion" dur="3" video=""
  while (( $# )); do case "$1" in
    --prompt) prompt="$2"; shift 2;; --out) out="$2"; shift 2;;
    --model) model="$2"; shift 2;; --duration) dur="$2"; shift 2;;
    --video) video="$2"; shift 2;;
    *) echo "unknown arg: $1" >&2; exit 2;; esac; done
  [[ -z "$prompt" || -z "$out" ]] && { echo "필수: --prompt, --out" >&2; exit 2; }
  need_editor
  echo "애니메이션 생성: model=$model out=$out dur=${dur}s video=${video:-<none>} (포인트 소모)"
  probe_call KickAnimation "new object[]{ $(dec "$(b64 "$prompt")"), $(dec "$(b64 "$out")"), $(dec "$(b64 "$model")"), ${dur}f, $(dec "$(b64 "$video")") }" >/dev/null
  poll "GEN:DONE" "GEN:ERROR" 360
}

cmd_spritesheet() {
  # 영상 모델(seedance/kling)로 참조이미지→애니 영상→프레임 시트 생성. --clip 주면 키잉+슬라이스+sprite-swap 클립까지 한 번에.
  # 영상 시트는 불투명 배경이라 key 기본 ON(--no-key 로 끔). ppu=100 정규화. fps 는 클립 재생 속도.
  local prompt="" sheet="" clip="" model="video-seedance-1-pro" loop="true" ref="" cols="4" rows="4" fps="12" ppu="100" key="true"
  while (( $# )); do case "$1" in
    --prompt) prompt="$2"; shift 2;; --sheet) sheet="$2"; shift 2;;
    --clip) clip="$2"; shift 2;; --model) model="$2"; shift 2;;
    --ref) ref="$2"; shift 2;; --cols) cols="$2"; shift 2;; --rows) rows="$2"; shift 2;;
    --fps) fps="$2"; shift 2;; --ppu) ppu="$2"; shift 2;; --no-key) key="false"; shift;;
    --no-loop) loop="false"; shift;;
    *) echo "unknown arg: $1" >&2; exit 2;; esac; done
  [[ -z "$prompt" || -z "$sheet" ]] && { echo "필수: --prompt, --sheet [--clip C] [--ref 첫프레임] [--cols N --rows M --fps F --ppu 100 --no-key]" >&2; exit 2; }
  need_editor
  echo "스프라이트시트 생성: model=$model sheet=$sheet clip=${clip:-<none>} ref=${ref:-<none>} grid=${cols}x${rows} fps=$fps loop=$loop ppu=$ppu key=$key (포인트 소모)"
  probe_call KickSpritesheet "new object[]{ $(dec "$(b64 "$prompt")"), $(dec "$(b64 "$sheet")"), $(dec "$(b64 "$clip")"), $(dec "$(b64 "$model")"), $loop, $(dec "$(b64 "$ref")"), $cols, $rows, ${fps}f, $ppu, $key }" >/dev/null
  poll "GEN:DONE" "GEN:ERROR" 360
}

cmd_clip() {
  # ppu=100: AI 생성 시트는 PPU=텍스처폭(1024)으로 들어와 ~10배 작게 렌더됨 → 표준 100 으로 정규화.
  # --key: 평평한 단색 배경(영상 시트 등)을 투명으로 키잉(이미 투명한 시트엔 불필요).
  local sheet="" out="" cols="4" rows="4" fps="12" loop="true" ppu="100" key="false"
  while (( $# )); do case "$1" in
    --sheet) sheet="$2"; shift 2;; --out) out="$2"; shift 2;;
    --cols) cols="$2"; shift 2;; --rows) rows="$2"; shift 2;;
    --fps) fps="$2"; shift 2;; --no-loop) loop="false"; shift;;
    --ppu) ppu="$2"; shift 2;; --key) key="true"; shift;;
    *) echo "unknown arg: $1" >&2; exit 2;; esac; done
  [[ -z "$sheet" || -z "$out" ]] && { echo "필수: --sheet, --out [--cols N --rows M --fps F --ppu 100 --key]" >&2; exit 2; }
  need_editor
  echo "클립 작성(AI 불필요·포인트 0): sheet=$sheet out=$out grid=${cols}x${rows} fps=$fps loop=$loop ppu=$ppu key=$key"
  probe_call BuildSpriteClip "new object[]{ $(dec "$(b64 "$sheet")"), $(dec "$(b64 "$out")"), $cols, $rows, ${fps}f, $loop, $ppu, $key }" >/dev/null
  get_status
}

case "${1:-}" in
  ensure) shift; cmd_ensure "$@";;
  models) shift; cmd_models "$@";;
  gen)    shift; cmd_gen "$@";;
  resize) shift; cmd_resize "$@";;
  sound)  shift; cmd_sound "$@";;
  anim)   shift; cmd_anim "$@";;
  spritesheet) shift; cmd_spritesheet "$@";;
  clip)   shift; cmd_clip "$@";;
  status) shift; get_status;;
  *) echo "usage: uai.sh {ensure|models [grep]|gen --prompt P --out O [--model M(기본 gemini-3.0-pro/Nanobanana Pro)] [--ref R] [--remove-bg]|sound --prompt P --out O [--model M] [--duration D] [--loop]|anim --prompt P --out O [--model M] [--duration D] [--video V]|spritesheet --prompt P --sheet S [--clip C] [--ref FirstFrame] [--cols N --rows M --fps F --ppu 100 --no-key]|clip --sheet S --out O [--cols N --rows M --fps F --ppu 100 --key --no-loop]|resize --src S --dst D --w W --h H|status}" >&2; exit 2;;
esac
