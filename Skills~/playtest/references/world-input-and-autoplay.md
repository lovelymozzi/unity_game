# 월드 입력 & 인게임 자동 플레이 — 게임별 레시피

playtest 스킬의 **코어**(캡쳐·introspect·좌표 tap/drag·리포팅)는 게임 무관하게 동작한다. 하지만 아래 둘은
게임의 입력 아키텍처·전략·씬 흐름에 결합되므로 파운데이션이 실행 스크립트로 싣지 않고 **패턴**으로 제공한다.
프로젝트에 맞게 구현해 `scripts/` 로 옮겨 쓴다.

---

## 1. 월드 입력 어댑터 (EventSystem 밖 게임 자체 입력)

인게임 보드/조준/이동처럼 게임이 `EventSystem` 이 아니라 **자체 입력 소스**를 읽는 경우 좌표 `tap` 은 NO_HIT 다.
해결책은 게임 입력 인터페이스의 **시뮬레이션 구현**을 런타임에 끼워 넣는 것이다.

**필요 요소 (게임 코드 측):**
- 게임 입력 인터페이스(예: `IInputHandler`)의 시뮬레이션 구현. 정적 필드로 좌표/터치상태를 노출하고 `#if UNITY_EDITOR` 로 가둔다.

```csharp
// 예시 — 실제 인터페이스/enum 이름은 게임마다 다르다.
#if UNITY_EDITOR
public sealed class SimulatedInputHandler : IInputHandler {
    public static Vector2   SimPosition;
    public static TouchType SimTouchType;   // DOWN / MOVE / UP / NONE 등 게임 enum
    public Vector2   Position => SimPosition;
    public TouchType Type     => SimTouchType;
}
#endif
```

**활성화(스왑) — eval 리플렉션:** 게임 입력 매니저의 private 핸들러 필드를 시뮬레이션 구현으로 교체.

```bash
. scripts/_cli.sh
ev 'var im = UnityEngine.Object.FindFirstObjectByType<YourInputManager>();
    typeof(YourInputManager)
      .GetField("_inputHandler", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
      .SetValue(im, new SimulatedInputHandler());
    return "swapped";'
```

**제스처(프레임 진행은 eval 사이 sleep 으로):**
```bash
ev 'SimulatedInputHandler.SimPosition = new UnityEngine.Vector2(375f, 950f);
    SimulatedInputHandler.SimTouchType = TouchType.DOWN; return "down";'
sleep 0.4
ev 'SimulatedInputHandler.SimTouchType = TouchType.MOVE; return "move";'
sleep 0.8
ev 'SimulatedInputHandler.SimTouchType = TouchType.UP;   return "up";'
ev 'SimulatedInputHandler.SimTouchType = TouchType.NONE; return "none";'   # UP 직후 즉시 NONE (단발 보장)
```

**복원(필수):** 끝나면 원래 핸들러로 되돌린다.
```bash
ev 'var im = UnityEngine.Object.FindFirstObjectByType<YourInputManager>();
    typeof(YourInputManager).GetField("_inputHandler", System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
      .SetValue(im, new RealInputHandler()); return "restored";'
```

**gotcha (게임 무관):**
- 발사/입력 트리거가 특정 상태(예: TURN_READY)에서만 처리되면 그 상태를 먼저 확인(`probe.sh "<C#>"`).
- 좌표계: 게임뷰 픽셀(좌하단 원점, y-up). 월드 조준은 `Camera.WorldToScreenPoint` 역산 결과를 `SimPosition` 에 넣는다.
- UP 을 여러 프레임 유지하면 재트리거(연발)될 수 있다 → UP 직후 즉시 NONE.

---

## 2. 인게임 자동 플레이 (실입력, 스테이지 진행)

봇/치트(로직 직접 호출)가 **아니라**, 게임의 AI/전략을 "결정 두뇌"로만 쓰고 결과를 **위 월드 입력 어댑터**로 실제 발사해
스테이지를 클리어/실패까지 자동 진행하는 패턴. 매 턴 다음을 반복한다:

1. **턴 준비 감지** — `probe.sh "<C#>"` 로 게임 상태가 입력 대기인지 확인.
2. **결정** — 게임 전략에서 발사 파라미터만 얻는다(발사는 하지 않음). 예: 각도/스왑/차지.
   - ⚠ 전략이 `async`/`await UniTask.Yield()` 를 쓰면 단발 eval 에서 동기 블로킹 → 데드락. **완전 동기 전략**을 쓰거나 결정 로직을 동기 경로로 노출한다.
3. **역산 + 자체검증** — 각도 → 화면 좌표. 발사 전 역변환이 원 결정과 일치하는지 eval 로 검증(드리프트 조기 발견).
4. **실입력 발사** — 월드 입력 어댑터의 down→move→up→none 시퀀스.
5. **관찰/로깅** — `probe.sh` + `capture.sh`, 리포트 모드면 `logstep.sh`.
6. **종료 조건** — 클리어/실패 팝업·목표 달성 감지. 안전 캡(예: 25턴)로 무한루프 방지.
7. **원복(실패해도 필수)** — 입력 핸들러 복원 + 변경한 레벨/상태 되돌리기 + `playmode.sh off`.

**템플릿:** `autoplay_turn.sh.txt` 를 게임에 맞게 채워 `scripts/autoplay_turn.sh` 로 옮긴다.

**정직하게 문서화할 것(드리프트/한계):**
- 진입 UI 이름·튜토리얼 딤 이름·조준 반경 등 **프리팹 의존 상수**는 프리팹 변경 시 깨진다 → 실패 시 최우선 확인 대상으로 명시.
- 전략이 특정 스테이지 유형을 못 푸는 것은 정상 결과일 수 있다(전략 한계 vs 버그 구분).
- 치트로 레벨/진행을 바꾸면 계정/서버에 이력이 남을 수 있다 → 원복 범위를 명확히.
