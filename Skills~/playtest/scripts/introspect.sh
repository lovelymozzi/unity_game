#!/usr/bin/env bash
# playtest: 활성 IPointerClickHandler 구현체(Button + 커스텀) 열거 → 좌표/타입/경로.
# 좌표는 게임뷰 픽셀 공간(좌하단 원점). Play Mode 필요(EventSystem.current).
set -euo pipefail
UEXEC="$(cd "$(dirname "$0")/../../unity-editor-ops/scripts" && pwd)/uexec.sh"
read -r -d '' CODE <<'CS' || true
var sb = new System.Text.StringBuilder();
var es = UnityEngine.EventSystems.EventSystem.current;
sb.Append("EventSystem=" + (es != null ? es.name : "NULL") + "\n");
var all = UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None);
int n = 0;
foreach (var mb in all) {
  if (mb == null) continue;
  if (!(mb is UnityEngine.EventSystems.IPointerClickHandler)) continue;
  if (!mb.gameObject.activeInHierarchy) continue;
  var rt = mb.transform as UnityEngine.RectTransform;
  if (rt == null) continue;
  if (n++ >= 40) break;
  var c = mb.GetComponentInParent<UnityEngine.Canvas>();
  UnityEngine.Camera cam = (c != null && c.renderMode != UnityEngine.RenderMode.ScreenSpaceOverlay) ? c.worldCamera : null;
  var sp = UnityEngine.RectTransformUtility.WorldToScreenPoint(cam, rt.position);
  var path = mb.gameObject.name;
  var pr = mb.transform.parent;
  for (int k = 0; k < 2 && pr != null; k++) { path = pr.name + "/" + path; pr = pr.parent; }
  sb.Append((int)sp.x + "," + (int)sp.y + " | " + mb.GetType().Name + " | " + path + "\n");
}
return sb.ToString();
CS
"$UEXEC" "$CODE"
