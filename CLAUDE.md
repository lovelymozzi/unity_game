# MatchDefense

<!-- hwi-foundation:start -->
# 개발 환경 및 기술 스택

1. **엔진 및 그래픽**: Unity 6 (실측 타깃 6000.3.16f1) · 2D
   - 모든 게임 오브젝트는 `SpriteRenderer`를 사용하며, 좌표계 및 화면 크기는 `MainCamera`의 `Orthographic Size`를 기반으로 동적 대응합니다.
   - 오쏘그래픽 뷰포트 동적 대응은 파운데이션 `Foundation.UI.OrthographicCameraFitter`를 사용합니다.
2. **UI 시스템**: `uGUI (Canvas)` + `Unity Screen Navigator`
   - 화면 전환 및 팝업 관리는 `Unity Screen Navigator`(`Page`, `Modal`, `Sheet`)를 기반으로 작성합니다. (‼ 베이스 클래스는 `Page`/`Modal` — `Screen` 아님. 네임스페이스 단수: `UnityScreenNavigator.Runtime.Core.Page` / `...Core.Modal`.)
   - 모든 텍스트 출력은 반드시 `TextMeshPro (TMP_Text)`를 사용합니다.

# 프로젝트 탑재 주요 패키지 및 활용 규칙

코드를 작성할 때 다음 패키지들의 API와 고유 설계 규칙을 반드시 준수해야 합니다.

1. **비동기 처리 (`UniTask`)**:
   - `Coroutine`이나 `async/await`(Task) 대신 `UniTask` / `UniTaskVoid`를 기본 비동기 패턴으로 사용합니다.
2. **에셋 및 메모리 관리 (`Addressables` + `Addler`)**:
   - 모든 프리팹, 사운드, 데이터 에셋은 Addressable 시스템 기반으로 비동기 로드합니다.
   - 에셋 수명 관리/자동 해제(Release)는 `Addler`로: `Addressables.LoadAssetAsync<T>(key).BindTo(gameObject).ToUniTask()`.
     - ‼ 확장 메서드는 **`.BindTo(handle, GameObject)`** 입니다(`.DisposeWith()` 아님 — 실측). 핸들을 반환하므로 체인 가능. 네임스페이스 `Addler.Runtime.Core.LifetimeBinding`.
     - **하드 선행조건:** `AddressableAssetSettings` + 최소 1개 그룹 + 주소 지정(없으면 진입점 전부 `InvalidKeyException`).
3. **이벤트 및 반응형 처리 (`R3`)**:
   - 상태 변화, UI 갱신, 글로벌 이벤트 발행/구독은 `R3`를 사용합니다(`Observable`, `ReactiveProperty`, `Subject`). 상태 소유자는 1곳, UI는 구독만(단방향). 구독은 `.AddTo(...)`로 수명 바인딩.
   - WebGL: 최소 표면만(`Subject`/`Subscribe`/`AsObservable`). `Interval`/`Delay`/`ObserveOn`/`ThreadPool` 금지. TimeProvider 교정은 R3.Unity가 담당.
4. **애니메이션 및 모션 (`DOTween`)**:
   - UI 페이드, 오브젝트 이동, 스케일 연출 등 모든 모션은 코루틴 대신 `DOTween` API로 작성합니다.
   - 일시정지/모달 위(timeScale=0) 경로는 `.SetUpdate(true)`(unscaled) + `Ease.Linear` 강제(freeze 회피). AOT: `DOTween.To`는 float/Color/Vector만(그 외 IL2CPP `ExecutionEngineException`).
5. **사운드 관리 (`LucidAudio`)**:
   - BGM/SFX 재생은 `LucidAudio` API로 제어합니다. 네임스페이스 `AnnulusGames.LucidTools.Audio`.
   - `AudioType`가 `UnityEngine.AudioType`와 CS0104 충돌 → alias/full-qualify. `SetAudioMixerGroup(...)`은 재생마다 재적용(Init 재사용 시 null 리셋).
6. **데이터 관리 (외부 CSV → DataManager)**:
   - 게임 내 밸런스/정적 데이터는 외부 CSV 파일로 분리하고, 게임 시작 시점에 `UniTask`로 비동기 로드하여 메모리에 캐싱하는 DataManager 구조를 포함합니다.
   - **v1.0.0:** 경량 순수 C# 리더 `CsvTable`(`Templates~/Scaffold/CsvTable.cs.txt`) 사용 — 런타임 리플렉션/소스젠 없음 → IL2CPP/WebGL 안전.
   - **v1.0.1(이월):** `CsvCSharp`(소스젠, `[CsvObject]` + `CsvSerializer.Deserialize`) via NuGetForUnity 로 교체(파일 인터페이스 동일 유지). CsvHelper는 채택 안 함(AOT/에디터-only 제약).

## 패키지 설치 시 필수 define & 함정 (v1.0.0 실측)

- **`USN_USE_ASYNC_METHODS`** (전 타깃 define, 필수): USN 라이프사이클을 `Task` 반환으로 전환(미설정 시 `IEnumerator` 코루틴). 뷰 오버라이드는 `public override async Task Initialize()`; 진입 애니메이션은 await 가능한 `WillPushEnter`(Task)에서 — `DidPushEnter`는 `void`(동기). async Task 본문에서 UniTask(`.ToUniTask()`)를 await할 수 있음.
- **`UNITASK_DOTWEEN_SUPPORT`** (전 타깃 define, 필수): DOTween(raw DLL)은 UniTask versionDefine 자동감지가 안 되므로 이 define로 `Tween.ToUniTask()` 확장을 켬.
- **Assembly Version Validation = OFF** / **WebGL managedStrippingLevel = High(3)** / **link.xml append**(덮어쓰기 금지). 자세히 → `Documentation~/M0_EDITOR_KICKOFF.md`.

# 코드 가이드라인 및 컨벤션

1. **명명 규칙 (C# Standard)**:
   - 클래스, 메서드, public 변수, 프로퍼티: `PascalCase`
   - private / protected 변수: `_camelCase` (언더스코어 접두사 필수)
2. **최적화 규칙**:
   - `Update` 내 `GetComponent`/`Find` 금지 — 초기화 시점(Awake)에 캐싱.
   - 자주 생성/삭제되는 오브젝트는 `UnityEngine.Pool.ObjectPool` + `UniTask` 풀링.

# 출력 요구사항 (변환/신규 구현 시)

1. **스크립트 아키텍처 및 패키지 연동 설계**:
   - 필요한 스크립트 목록·각 역할·사용 패키지(R3, Addler, UniTask 등)를 요약한 테이블/리스트를 먼저 제공.
2. **완전한 C# 소스 코드 제공**:
   - 유니티에서 에러 없이 컴파일 가능한 완성 코드(생략 코드 `// 생략...` 지양).
   - 역할에 따라 분리:
     - **DataManager**: CSV 파싱(v1.0.0=경량 `CsvTable`, v1.0.1=CsvCSharp) 및 제공 로직
     - **Core/Manager**: 게임 흐름 제어, `R3` 이벤트/상태 관리, `Addler` 기반 에셋 로더
     - **Controller/Object**: 오브젝트 이동·충돌, `DOTween` 연출, `UnityEngine.Pool` 풀링
     - **UI/View**: `Unity Screen Navigator`(`Page`/`Modal`) + `TMP` 연동 UI 뷰 로직

---

## 4계층 스캐폴드 (파운데이션 제공)

`Templates~/Scaffold/` 아래 계층별 시작 코드가 있습니다. 복사 후 `.cs.txt` → `.cs`로 확장자를 바꿔 사용하세요. (v1.0.0 에디터 실측으로 API 확정 — 컴파일 green.)

| 계층 | 스캐폴드 파일 | 사용 패키지 |
|---|---|---|
| DataManager | `CsvTable.cs.txt`, `DataManager.cs.txt` | 경량 CSV(v1.0.0) → CsvCSharp(v1.0.1) + UniTask |
| Core/Manager | `GameManager.cs.txt`, `FoundationBootstrap.cs.txt` | R3 + Addler + UniTask + Addressables |
| Controller/Object | `EnemyActor.cs.txt` | DOTween + SpriteRenderer + `UnityEngine.Pool` + UniTask |
| UI/View | `MainScreen.cs.txt`, `ConfirmModal.cs.txt`, `ModalHost.cs.txt` | Unity Screen Navigator + TMP + DOTween |
<!-- hwi-foundation:end -->

<!-- hwi-unity-cli-skill:start -->
## Unity 에디터 조작 — 공식 Unity CLI

이 프로젝트는 Unity 에디터 조작·검증·AI 생성을 **공식 Unity CLI**(`unity` 바이너리 + `com.unity.pipeline` 패키지)로 한다.
스킬은 `.claude/skills/` 에 설치되어 있다:

- `unity-cli` — 에디터 구동·검증·조작(전송수단 정본). 사용법·검증 게이트·안전 수칙.
- `playtest` — 좌표 기반 Play Mode 테스트 + 검증 리포트.
- `unity-ai-image-gen` — Unity AI Generators 로 이미지/사운드/애니 생성.

핵심 커맨드:
- 상태: `unity status --format json` (에디터+Pipeline 서버가 떠 있어야 함)
- C# 실행: `unity command eval --project-path . --format json 'return UnityEngine.Application.unityVersion;'`
- 컴파일 검증(.cs 수정 후 필수): `unity command recompile --project-path .` → `unity command recompile_status --project-path . --format json`
- 상세 규칙: `.claude/skills/unity-cli/SKILL.md`
<!-- hwi-unity-cli-skill:end -->
