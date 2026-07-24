#!/usr/bin/env bash
# playtest: 이름/경로에 <substr>(대소문자 무시)가 포함된 활성 클릭가능 요소를 직접 클릭.
# RaycastAll 스택을 무시하므로, 딤/팝업 중첩에 가려져 좌표 탭이 빗나가는 경우에 강함.
# 구체적 substr 권장(예: "ButtonGroup/Button", "Home", "Retry"). 첫 매치를 클릭. Play Mode 필요.
set -euo pipefail
SUB="$1"
. "$(cd "$(dirname "$0")" && pwd)/_cli.sh"
CODE="var es = UnityEngine.EventSystems.EventSystem.current; if (es == null) return \"NO_EVENTSYSTEM (play mode 필요)\"; var sub = \"$SUB\".ToLower(); UnityEngine.GameObject hit = null; string hitPath = null; foreach (var mb in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None)) { if (mb == null) continue; if (!(mb is UnityEngine.EventSystems.IPointerClickHandler)) continue; if (!mb.gameObject.activeInHierarchy) continue; var path = mb.gameObject.name; var pr = mb.transform.parent; for (int k = 0; k < 3 && pr != null; k++) { path = pr.name + \"/\" + path; pr = pr.parent; } if (path.ToLower().Contains(sub)) { hit = mb.gameObject; hitPath = path; break; } } if (hit == null) return \"NOT_FOUND: \" + sub; var ped = new UnityEngine.EventSystems.PointerEventData(es); UnityEngine.EventSystems.ExecuteEvents.Execute(hit, ped, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler); return \"clicked: \" + hitPath;"
ev "$CODE"
