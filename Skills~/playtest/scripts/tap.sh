#!/usr/bin/env bash
# playtest: 좌표 (x,y)를 탭. RaycastAll로 히트 확인 후 ExecuteEvents down/up/click.
# 좌표는 게임뷰 픽셀 공간(좌하단 원점). Play Mode 필요.
set -euo pipefail
X="$1"; Y="$2"
. "$(cd "$(dirname "$0")" && pwd)/_cli.sh"
CODE="var es = UnityEngine.EventSystems.EventSystem.current; if (es == null) return \"NO_EVENTSYSTEM (play mode 필요)\"; var ped = new UnityEngine.EventSystems.PointerEventData(es){ position = new UnityEngine.Vector2($X,$Y) }; var res = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>(); es.RaycastAll(ped, res); if (res.Count == 0) return \"NO_HIT ($X,$Y)\"; var go = res[0].gameObject; ped.pointerCurrentRaycast = res[0]; ped.pointerPressRaycast = res[0]; var h = UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(go); UnityEngine.EventSystems.ExecuteEvents.Execute(h, ped, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler); UnityEngine.EventSystems.ExecuteEvents.Execute(h, ped, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler); UnityEngine.EventSystems.ExecuteEvents.Execute(h, ped, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler); return \"OK top=\" + go.name + \" handler=\" + (h != null ? h.name : \"null\");"
ev "$CODE"
