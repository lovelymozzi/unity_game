#!/usr/bin/env bash
# playtest: 안 보이는 데이터(모션/데이터 흐름) 프로브. uexec로 C# 조회 → .data(JSON) 반환.
# 사용:
#   probe.sh --standard         표준 세트(scene, isPlaying, 재생중 Animator, 활성 ParticleSystem)
#   probe.sh "<C# 식/문 + return>"  임의 조회 (게임 특정 상태/데이터는 여기서)
set -euo pipefail
DIR="$(cd "$(dirname "$0")/../../unity-editor-ops/scripts" && pwd)"
UEXEC="$DIR/uexec.sh"

if [[ "${1:---standard}" == "--standard" ]]; then
  read -r -d '' CODE <<'CS' || true
var playing = new System.Collections.Generic.List<string>();
foreach (var a in UnityEngine.Object.FindObjectsByType<UnityEngine.Animator>(UnityEngine.FindObjectsSortMode.None)) {
  try {
    if (a == null || !a.isActiveAndEnabled || a.runtimeAnimatorController == null || a.layerCount == 0) continue;
    var st = a.GetCurrentAnimatorStateInfo(0);
    var ci = a.GetCurrentAnimatorClipInfo(0);
    string cn = ci.Length > 0 ? ci[0].clip.name : "?";
    playing.Add(a.gameObject.name + ":" + cn + "@" + st.normalizedTime.ToString("0.00"));
  } catch { }
}
var activePs = new System.Collections.Generic.List<string>();
foreach (var p in UnityEngine.Object.FindObjectsByType<UnityEngine.ParticleSystem>(UnityEngine.FindObjectsSortMode.None)) {
  if (p != null && p.isPlaying) activePs.Add(p.gameObject.name);
}
return new {
  scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
  isPlaying = UnityEditor.EditorApplication.isPlaying,
  playingAnimators = playing,
  activeParticles = activePs
};
CS
else
  CODE="$1"
fi

RESP="$("$UEXEC" "$CODE")"
if command -v jq >/dev/null 2>&1; then
  echo "$RESP" | jq '.data // .'
else
  echo "$RESP"
fi
