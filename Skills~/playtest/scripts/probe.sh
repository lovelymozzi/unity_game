#!/usr/bin/env bash
# playtest: 안 보이는 데이터(모션/데이터 흐름) 프로브. eval로 C# 조회 → 반환값(JSON).
# 사용:
#   probe.sh --standard         게임 무관 표준 세트(scene, isPlaying, 재생중 Animator, 활성 ParticleSystem)
#   probe.sh "<C# 식/문 + return>"  임의 조회 (게임 고유 상태/재화/상태머신 등은 이 형태로 프로젝트에 맞게)
set -euo pipefail
. "$(cd "$(dirname "$0")" && pwd)/_cli.sh"

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

RESP="$(ev "$CODE")"
if command -v jq >/dev/null 2>&1; then
  echo "$RESP" | jq . 2>/dev/null || echo "$RESP"
else
  echo "$RESP"
fi
