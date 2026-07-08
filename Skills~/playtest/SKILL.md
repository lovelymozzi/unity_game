---
name: playtest
description: Use to run coordinate-based play-mode tests on a Unity game via unity-exec — capture the screen, locate uGUI elements, tap/drag at coordinates, and verify results. Can emit a verification report folder (annotated screenshots + non-visual data probes + report.md). Trigger on "이 화면에서 X 눌러 Y 되는지 확인", "UI 동작 테스트", "팝업 열리는지 봐줘", "버튼 눌러봐", "검증 리포트 만들어", "모션 나왔는지 확인", or play-testing a uGUI flow.
---

# Playtest (좌표 기반 Play Mode 자동 테스트)

unity-exec로 게임 화면을 **캡쳐 → 이해 → 좌표 터치/입력 → 결과 관찰**하는 루프. uGUI 경로는 0 풋프린트(C# 템플릿 POST)로 동작한다. Claude가 스크린샷을 보고 자연어 의도를 좌표로 변환해 구동한다.

> 이 문서는 **자족적**이다 — 이 SKILL.md + scripts/ 만으로 uGUI 테스트 전체가 동작한다(메모리/PKM 불필요).
> 전송수단은 파운데이션 번들 `unity-editor-ops`(unity-exec) 스킬을 재사용한다.

## When to use
- uGUI UI 동작 검증(화면 전환·팝업·버튼·HUD 등).
- 검증 리포트(주석 스샷 + 비시각 프로브 + report.md) 산출.

## When NOT to use
- 멀티터치 제스처(핀치 등): 에디터 마우스 경로라 불가.
- **비-uGUI 월드 입력**(EventSystem 밖 게임 자체 입력 읽기): 게임별 입력 어댑터가 필요 → 아래 "월드 입력" 참고.

## 전제
- unity-exec 실행 중(미실행 시 `unity-editor-ops/scripts/launch.sh`).
- **tap/drag/introspect는 Play Mode 필요**(런타임 `EventSystem`). capture는 edit/play 모두.
- 에디터가 포커스를 잃으면 Play Mode가 종료될 수 있음 → `playmode.sh on`으로 재진입.

## 루프 절차 (uGUI)
1. `scripts/playmode.sh on` — Play 진입 + 도메인 리로드 재연결 대기.
2. `scripts/capture.sh [path]` — 화면 PNG → Read로 이해.
3. `scripts/introspect.sh` — 활성 클릭가능 요소 좌표/타입/경로.
4. `scripts/tap.sh x y` (좌표) 또는 `scripts/tapname.sh <이름조각>` (스택 무시 직접 클릭) 또는 `scripts/drag.sh x1 y1 x2 y2`.
5. `scripts/observe.sh` + `scripts/capture.sh`(after).
6. 전/후 비교로 판정. 반복.

## 리포팅 모드 (검증 리포트 산출)
"검증 리포트/기록 남겨서/모션 확인" 류 요청 시: 위 루프에 **기록 레이어**를 얹어 스샷+비시각 데이터+리포트를 한 폴더로 묶는다. 산출물은 `claudedocs/playtest-reports/<날짜>-<slug>/` (gitignore, 로컬 전용).

1. `scripts/newrun.sh <slug> "테스트 의도"` → RUNDIR 경로 반환(폴더 + `report.md`/`progress.md`/`shots/` 스켈레톤).
2. **스텝마다 반복**:
   - `scripts/capture.sh "$RUNDIR/shots/NN-before.png"` (액션 전)
   - `scripts/annotate.sh <before> <before> --tap X Y` (또는 `--arrow X1 Y1 X2 Y2`) `--label "NN 액션명"` — **빨간 표기는 누를 대상이 보이는 before에 찍는다**. 좌표는 클릭공간(y-up), 변환은 자동.
   - 액션 수행 (`tap`/`tapname`/`drag`)
   - `scripts/capture.sh "$RUNDIR/shots/NN-after.png"` (액션 후 — **결과 증거, 표기 없이 깨끗이**)
   - `scripts/probe.sh --standard` (필요 시 `probe.sh "<C#>"` 추가) — 안 보이는 데이터 수집.
   - `scripts/logstep.sh "$RUNDIR" NN "액션" "관찰" "$(probe 결과)" "shots/NN-before.png"` — progress.md 누적.
3. 끝나면 `progress.md`를 종합해 `report.md` 작성(성공/실패·근거·데이터 흐름·모션·원인·재현 메모).
4. **마무리(필수)**: `scripts/playmode.sh off`로 **Play 모드를 끈다**. (테스트 종료 = 상태 원복까지)

**모션/비시각 검증**: 화면 단일 프레임으로 모션 재생을 직접 못 보므로 `probe`로 결정적 확인 — `--standard`가 재생중 `Animator`(이름:클립@normalizedTime)·활성 `ParticleSystem`을 찍는다. 특정 모션은 액션 전/후 probe의 Animator·파티클 차이로 판정. **게임 고유 상태/데이터(점수·재화·상태머신 등)는 `probe.sh "<C#>"`로 프로젝트에 맞게 조회**한다.

## 좌표 규약
- 게임뷰 픽셀 공간(예 750×1334). introspect/tap/capture 동일 공간.
- 클릭 좌표 = introspect 값 그대로(좌하단 원점, y-up).
- 스크린샷(좌상단 원점) 상관 시: `y_img = H − y_click`.
- `Screen.width/height`는 에디터에서 신뢰 불가 → `RectTransformUtility`/스크린샷 해상도 기준.

## 월드 입력 (비-uGUI / 인게임 조준·이동) — 게임별 어댑터
인게임 보드/조준/이동 등 **EventSystem 밖에서 게임이 자체 입력을 읽는** 경우 좌표 `tap`은 NO_HIT다. 이는 게임의 입력 아키텍처에 결합되므로 **프로젝트가 어댑터를 제공**한다(일반 패턴):

- 게임 입력 인터페이스의 **시뮬레이션 구현**(예: `SimulatedInputHandler : IYourInputHandler`, 정적 `SimPosition`/`SimTouchType`, `#if UNITY_EDITOR`)을 프로젝트에 추가.
- 런타임에 게임 입력 소스를 리플렉션으로 스왑: `FindFirstObjectByType<YourInputManager>()` 의 private 핸들러 필드를 `SetValue(..., new SimulatedInputHandler())`.
- uexec 호출 사이 sleep으로 프레임을 진행시키며 down→move→up 시퀀스를 구성, 끝나면 원래 핸들러로 복원.

> 구체 클래스/필드명은 게임마다 다르다 → 이 스킬은 uGUI 좌표 경로만 코어로 제공하고, 월드 입력은 위 패턴을 게임에 맞게 구현한다.

## 경계 / 주의 (실전 gotcha)
- 좌표 `tap`은 **EventSystem 핸들러(IPointerClickHandler 등)를 거치는 것**에만 작동(Button/커스텀).
- **비-uGUI game-touch 요소** → 좌표 탭 NO_HIT → 위 "월드 입력" 어댑터 패턴.
- **팝업/딤 중첩** 시 좌표 탭이 딤에 맞을 수 있음(raycast sort 순서) → **`tapname.sh <이름조각>`** 으로 해당 요소 직접 클릭.
- Play 진입 시 도메인 리로드로 unity-exec 잠깐 끊김 → `playmode.sh`가 재연결 폴링.
- 긴 인라인 C#은 unity-exec 보안정책 namespace-유사 패턴 오검출 주의(스크립트는 검증된 템플릿 사용).

## Scripts
- `scripts/playmode.sh on|off` — Play 진입/종료 + 재연결 폴링
- `scripts/capture.sh [path]` — ScreenCapture → PNG (기본 `/tmp/uitest/shot.png`)
- `scripts/introspect.sh` — 클릭가능 요소 열거(좌표·타입·경로)
- `scripts/tap.sh x y` — 좌표 RaycastAll + ExecuteEvents 탭
- `scripts/tapname.sh <substr>` — 이름/경로 매치 요소 직접 클릭(스택/딤 무시)
- `scripts/drag.sh x1 y1 x2 y2` — begin/drag/end 드래그
- `scripts/observe.sh` — 콘솔 에러 + 활성 씬 + isPlaying
- `scripts/newrun.sh <slug> ["의도"]` — 리포트 run 폴더 생성, RUNDIR 반환
- `scripts/annotate.sh <in> <out> [--tap x y] [--arrow x1 y1 x2 y2] [--label "txt"]` — 스샷 빨간 표기(PIL 필요, 좌표 y-up 자동 변환)
- `scripts/probe.sh --standard | "<C#>"` — 비시각 데이터 프로브(scene·isPlaying·Animator·파티클 / 임의 C#) → `.data` JSON

포트/토큰/JSON 처리는 파운데이션 번들 `unity-editor-ops/scripts/uexec.sh`·`resolve-port.sh` 재사용.
`annotate.py`는 Python3 + Pillow(PIL) 필요(`pip install Pillow`).
