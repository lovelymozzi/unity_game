<!--
  HWI Foundation — 신규 프로젝트 컨벤션 템플릿 (INSTALL-TIME TEMPLATE)

  이 파일은 파운데이션 패키지가 강제하는 "개발 환경·기술 스택·코드 컨벤션·출력 요구사항"의
  단일 출처(single source of truth)다. 신규 프로젝트 생성 시 이 파일을 프로젝트 루트의
  CLAUDE.md(또는 docs/CONVENTIONS.md)로 복사하고, AI/팀원이 코드를 작성할 때 반드시 준수한다.

  복사: cp Packages/com.hwi.foundation/Templates~/ProjectConventions.md <project>/CLAUDE.md
  (기존 CLAUDE.md가 있으면 이 내용을 append.)
-->

# 개발 환경 및 기술 스택

1. **엔진 및 그래픽**: Unity 2D (최신 LTS 버전)
   - 모든 게임 오브젝트는 `SpriteRenderer`를 사용하며, 좌표계 및 화면 크기는 `MainCamera`의 `Orthographic Size`를 기반으로 동적 대응되도록 합니다.
   - 오쏘그래픽 뷰포트 동적 대응은 파운데이션 `Foundation.UI.OrthographicCameraFitter`를 사용합니다.
2. **UI 시스템**: `uGUI (Canvas)` + `Unity Screen Navigator`
   - 화면 전환 및 팝업 관리는 `Unity Screen Navigator` 시스템(`Modal`, `Screen`)을 기반으로 작성해 주세요.
   - 모든 텍스트 출력은 반드시 `TextMeshPro (TMP_Text)`를 사용합니다.

# 프로젝트 탑재 주요 패키지 및 활용 규칙

코드를 작성할 때 다음 패키지들의 API와 고유 설계 규칙을 반드시 준수해야 합니다.

1. **비동기 처리 (`UniTask`)**:
   - `Coroutine`이나 `async/await`(Task) 대신 `UniTask` / `UniTaskVoid`를 기본 비동기 패턴으로 사용해 주세요.
2. **에셋 및 메모리 관리 (`Addressables` + `Addler`)**:
   - 모든 프리팹, 사운드, 데이터 에셋은 Addressable 시스템 기반으로 비동기 로드합니다.
   - Addressable 에셋의 생명주기 관리 및 자동 해제(Release)를 위해 `Addler` 패키지(예: `.DisposeWith()`, `Addressables.LoadAssetAsync().ToUniTask()`)를 활용하는 구조로 작성해 주세요.
3. **이벤트 및 반응형 처리 (`R3`)**:
   - 상태 변화, UI 갱신, 글로벌 이벤트 발행 및 구독은 `R3` (Reactive Extensions for Unity)를 사용해 주세요. (`Observable`, `ReactiveProperty` 활용)
4. **애니메이션 및 모션 (`DOTween`)**:
   - UI 페이드, 오브젝트 이동, 스케일 연출 등 모든 모션은 코루틴 대신 `DOTween` API를 연동하여 작성해 주세요.
5. **사운드 관리 (`Lucid Audio`)**:
   - BGM, SFX 등의 오디오 재생 로직은 `LucidAudio` API를 사용하여 제어해 주세요.
6. **데이터 관리 (`CsvHelper` / CsvCSharp via NuGet)**:
   - 게임 내 밸런스 데이터 및 정적 데이터는 외부 CSV 파일 구조로 분리합니다.
   - CSV 데이터를 파싱하고, 게임 시작 시점에 메모리에 로드(`UniTask` 활용)하여 캐싱하는 데이터 매니저 구조를 포함해 주세요.
   - **주의(파운데이션 v1.0.0):** CsvCSharp/NuGetForUnity는 v1.0.1로 이월됨(Unsafe 충돌·AOT 미증명·사용처 0). v1.0.0에서는 경량 `CsvTable` 리더(`Templates~/Scaffold/CsvTable.cs.txt`)로 시작하고, 두 실빌드(WebGL+Android IL2CPP) green 후 CsvCSharp `[CsvObject]` + `CsvSerializer.Deserialize`로 교체합니다.

# 코드 가이드라인 및 컨벤션

1. **명명 규칙 (C# Standard)**:
   - 클래스, 메서드, public 변수, 프로퍼티: `PascalCase`
   - private / protected 변수: `_camelCase` (언더스코어 접두사 필수)
2. **최적화 규칙**:
   - `Update` 메서드 내에서의 `GetComponent`나 `Find` 연산은 금지하며, 초기화 시점에 캐싱해야 합니다.
   - 자주 생성/삭제되는 오브젝트는 `UnityEngine.Pool.ObjectPool`과 `UniTask`를 연계한 풀링 구조를 취합니다.

# 출력 요구사항 (변환/신규 구현 시)

1. **스크립트 아키텍처 및 패키지 연동 설계**:
   - 필요한 스크립트 목록과 각각의 역할, 그리고 어떤 패키지(R3, Addler, UniTask 등)를 사용하는지 요약한 테이블/리스트를 먼저 제공.
2. **완전한 C# 소스 코드 제공**:
   - 유니티에서 에러 없이 컴파일 가능한 완성 코드(생략 코드 `// 생략...` 지양).
   - 역할에 따라 분리:
     - **DataManager**: CSV 데이터 파싱(v1.0.0=경량 `CsvTable`, v1.0.1=CsvCSharp) 및 제공 로직
     - **Core/Manager**: 게임 흐름 제어, `R3`를 활용한 이벤트/상태 관리, `Addler` 기반 에셋 로더
     - **Controller/Object**: 개별 오브젝트의 이동·충돌, `DOTween` 연출, `UnityEngine.Pool` 풀링
     - **UI/View**: `Unity Screen Navigator` 및 `TMP` 연동 UI 뷰 로직

---

## 4계층 스캐폴드 (파운데이션 제공)

`Templates~/Scaffold/` 아래에 계층별 시작 코드가 있습니다. 새 프로젝트에서 복사 후 `.cs.txt` → `.cs`로 확장자를 바꿔 사용하세요.

| 계층 | 스캐폴드 파일 | 사용 패키지 |
|---|---|---|
| DataManager | `CsvTable.cs.txt`, `DataManager.cs.txt` | 경량 CSV(v1.0.0) → CsvCSharp(v1.0.1) + UniTask |
| Core/Manager | `GameManager.cs.txt`, `FoundationBootstrap.cs.txt` | R3 + Addler + UniTask + Addressables |
| Controller/Object | `EnemyActor.cs.txt` | DOTween + SpriteRenderer + `UnityEngine.Pool` + UniTask |
| UI/View | `MainScreen.cs.txt`, `ConfirmModal.cs.txt` | Unity Screen Navigator + TMP + DOTween |

> 스캐폴드의 `⚠ VERIFY` 주석은 라이브러리 API명/시그니처가 에디터 실측 전(플랜 §12/§16.3)이라는 표시입니다. M0 에디터 기동 후 실 API로 확정하세요.
