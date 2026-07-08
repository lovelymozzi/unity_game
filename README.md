# HWI Foundation

> Unity 6 신규 프로젝트용 **thin adopt/bootstrap** 파운데이션. 모바일 2D(SpriteRenderer·Orthographic) 메인 + WebGL.

**Version:** 1.0.0
**Unity:** 6000.0+ (실측 타깃 6000.3.16f1)
**타깃:** iOS·Android 2D + WebGL / IL2CPP / .NET Standard 2.1

v1.0.0 은 **검증 라이브러리로 현대화**한 기준선이다. 파운데이션은 라이브러리를 감싸는 두꺼운 래퍼가 아니라 **설치·초기화·컨벤션·스캐폴딩 제공자**이며, 게임 코드는 라이브러리 네이티브 API 를 직접 쓴다.

| 표준 | 채택 |
|---|---|
| 비동기 | UniTask / UniTaskVoid (Coroutine·Task 지양) |
| 이벤트·반응형 | **R3** (Observable / ReactiveProperty) |
| 에셋 수명 | **Addressables + Addler** (`.DisposeWith()`) |
| 모션 | **DOTween** (모든 페이드·이동·스케일) |
| 오디오 | **LucidAudio** (BGM/SFX) |
| 화면·모달 | **Unity Screen Navigator** (Screen / Modal) + TMP |
| 데이터 | 외부 CSV — 경량 `CsvTable`(v1.0.0) → CsvCSharp(v1.0.1) |
| 풀링 | `UnityEngine.Pool.ObjectPool` + UniTask |

## 유지 모듈 (11)

| Module | Purpose | Key types |
|---|---|---|
| `Foundation.Core` | 공통 인터페이스·유틸·로거 컨텍스트 | `Result<T>`, `Option<T>`, `IFoundationLogger`, `FoundationContext` |
| `Foundation.Pool` | 오브젝트 풀 | `PrefabPool`, `PrefabPoolGroup` |
| `Foundation.UI` | UI·해상도·오쏘그래픽 | `CanvasScalerPreset`, `SafeAreaFitter`, `LetterboxController`, `OrthographicCameraFitter` |
| `Foundation.Mobile` | 모바일 부트스트랩 | `MobileBootstrap`, `LowMemoryDispatcher`, `ThermalMonitor`, `DeviceTier` |
| `Foundation.Async` | UniTask 헬퍼(얇게 유지) | `AwaitableInterop`, `DelayAsync`, `MonoBehaviourExtensions`, `UniTaskExtensions` |
| `Foundation.Scene` | 씬 로더 + lifecycle hook | `SceneLoader`, `ITransition`, `IOnSceneReady`, `IAfterSceneLoaded` |
| `Foundation.Logging` | Logger 구현 | `UnityDebugFoundationLogger`, `IngameDebugConsoleAdapter` (OSS optional) |
| `Foundation.Assets` | Addressables 래퍼 | `AssetHandle<T>`, `AssetGroup`, `AssetKeys` |
| `Foundation.Save` | 영속 저장 | `ISaveStore`, `PlayerPrefsSaveStore`, `JsonFileSaveStore`, `SaveContext` |
| `Foundation.Localization` | i18n | `LocalizationTable`, `Locale` |
| `Foundation.Input` | InputSystem wrap | `ActionMapBinder` |

> **v1.0.0 에서 제거된 모듈** — 네이티브 직접 사용으로 대체(자세히 → `Documentation~/MIGRATION_v1.md`):
> `Foundation.Audio` → LucidAudio · `Foundation.Events` → R3 · `Foundation.Popup` → USN Modal · `FadeAsync`/`FadeTransition` 모션 → DOTween.

---

## 설치 계약 (opt-in recipe)

⚠ **package.json 은 UPM 레지스트리 의존만 표현**한다. git-URL(Addler/USN/LucidAudio)·scopedRegistry(R3)·전역 define·Player 설정·link.xml 은 package.json 으로 안 따라간다. **아래 레시피가 유일한 계약이다** — 신규 프로젝트에서 수동 적용.

> 대상 = **신규 v1.0.0 프로젝트**. 라이브 ShootGame(임베드 사본 v0.5 동결)에는 지금 적용 금지.

### 1. `Packages/manifest.json`
`Templates~/manifest.snippet.json` 를 병합:

```json
"scopedRegistries": [
  { "name": "package.openupm.com", "url": "https://package.openupm.com",
    "scopes": ["com.cysharp.r3", "org.nuget"] }
],
"dependencies": {
  "com.hwi.foundation": "file:../LocalPackages/com.hwi.foundation",
  "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
  "com.cysharp.r3": "1.3.1",
  "org.nuget.r3": "1.3.1",
  "com.harumak.addler": "https://github.com/Haruma-K/Addler.git?path=/Assets/Addler#1.0.1",
  "com.harumak.unityscreennavigator": "https://github.com/Haruma-K/UnityScreenNavigator.git?path=/Assets/UnityScreenNavigator#v1.7.5",
  "com.annulusgames.lucid-audio": "https://github.com/AnnulusGames/LucidAudio.git?path=/Assets/LucidAudio#v1.0.1",
  "com.unity.addressables": "2.9.1",
  "com.unity.inputsystem": "1.19.0"
}
```

- **R3 는 OpenUPM 단일 채널만** (`com.cysharp.r3` + `org.nuget.r3`). NuGetForUnity 로 R3 재설치 금지(R3.dll 이중반입 → 컴파일 붕괴).
- scope 는 `com.cysharp.r3` 로 좁힘(broad `com.cysharp` 는 git-pinned UniTask 재라우팅 위험).
- ref 정확성: USN `#v1.7.5`(접두사 `v`, `#1.7.5` 는 404), LucidAudio `?path=/Assets/LucidAudio`(선두 슬래시), Addler `#1.0.1`(OpenUPM 는 조용히 1.0.0 → git-URL 만).
- `packages-lock.json` 커밋으로 해시 핀.

### 2. DOTween (UPM 없음 — 원자 커밋)
Asset Store free core import → **Tools ▸ Demigiant ▸ DOTween Utility Panel ▸ Setup DOTween** → **Create ASMDEF** (생성명 `DOTween.Modules`). 소스 + `DOTween.Modules.asmdef` + `DOTweenSettings.asset` + `.meta` 를 **한 커밋에** 넣는다(`Assets/Plugins/DOTween` gitignore 금지). loose 소스=firstpass 컴파일 → asmdef 참조 불가 = 치명.

### 3. Player Settings (커밋)
- **Assembly Version Validation = OFF** — 안 하면 R3 TimeProvider 8.0.0 이 IL2CPP `FileLoadException`.
- **Scripting Define Symbols — 전 타깃 그룹**(Standalone/WebGL/**Android/iOS**):
  - `EXCLUDE_COMPILER_SERVICES_UNSAFE` — `System.Runtime.CompilerServices.Unsafe` 이미 존재(Burst/AI Assistant/Collections). 재반입 시 *"Multiple precompiled assemblies with the same name"* → 에디터 전체 컴파일 붕괴. (Assembly Version Validation OFF 로는 해결 안 됨 — 동일 파일명 문제.)
- **WebGL managedStrippingLevel = High(3)**, `stripEngineCode = 1`.

### 4. `link.xml` (append, 덮어쓰기 금지)
`Templates~/link.xml` 항목을 **기존 link.xml 에 추가**. 기존 `Unity.InputSystem`·`Unity.TextMeshPro` preserve 를 덮으면 재스트립 → WebGL 런타임 붕괴. 어셈블리명은 각 라이브러리 import 후 실측(§16.3): Addler=`Addler`, DOTween=`DOTween.Modules`, USN=`UnityScreenNavigator`, LucidAudio=`AnnulusGames.LucidAudio.Runtime`, R3=`R3`/`R3.Unity`/`Microsoft.Bcl.TimeProvider`.

### 5. asmdef 배선
26 named asmdef 는 autoReferenced 를 상속하지 않는다. 소비 게임 asmdef 마다 `Foundation.*` + 채택 라이브러리 어셈블리를 **명시 참조**.

### 6. CSV — v1.0.0 은 경량 리더
CsvCSharp + NuGetForUnity 는 **v1.0.1 로 이월**(Unsafe 4중충돌·AOT 미증명·사용처 0). v1.0.0 은 `Templates~/Scaffold/CsvTable.cs.txt`(순수 C#) 로 시작하고, 두 실빌드(WebGL+Android IL2CPP) green 후 교체.

---

## 채택 라이브러리 컨벤션 (thin)

파운데이션은 아래 규약만 강제하고, 코드는 네이티브 API 직접 사용.

- **R3** — 상태=`ReactiveProperty`, 이벤트=`Subject`, 구독은 `.AddTo(...)` 로 수명 바인딩. WebGL 은 최소 표면(Subject/Subscribe/AsObservable); `Interval`/`Delay`/`ObserveOn`/`ThreadPool` 금지. provider 초기화는 R3.Unity 가 담당(부트스트랩에서 assert).
- **Addler** — `Addressables.LoadAssetAsync<T>(key).ToUniTask()` + `.DisposeWith(gameObject/token)`. **하드 선행조건: `AddressableAssetSettings` + 최소 1그룹 + 주소 지정**(없으면 진입점 전부 `InvalidKeyException`).
- **DOTween** — 컴포넌트 shortcut(`DOMove`/`DOScale`/`DOFade`) 위주. 팝업/Pause 경로는 `.SetUpdate(true)` + `Ease.Linear` 강제(timeScale=0 freeze 회피). `DOTween.To` 는 float/Color/Vector 만(그 외 IL2CPP ExecutionEngineException).
- **USN** — `Screen`/`Modal` 직접, 네비게이션 결과는 `AsyncProcessHandle.Task.AsUniTask()`. Popup 폐기.
- **LucidAudio** — namespace `AnnulusGames.LucidTools.Audio`. `SetAudioMixerGroup(...)` 매 재생 재적용(Init 재사용 시 null 리셋). `AudioType{BGM,SE}` 는 `UnityEngine.AudioType` 와 CS0104 충돌 → full-qualify.

## 신규 프로젝트 스캐폴드

`Templates~/` 는 Unity 가 무시하는(`~`) 폴더 — 컴파일 안 됨. 복사해서 사용:

1. `cp Templates~/ProjectConventions.md <project>/CLAUDE.md` — 개발 환경·컨벤션·출력 요구사항(단일 출처).
2. `Templates~/Scaffold/*.cs.txt` → `Assets/Scripts/` 로 복사 후 `.cs` 로 확장자 변경. 4계층(DataManager/Core-Manager/Controller-Object/UI-View) 시작 코드 + 샘플 CSV. 자세히 → `Templates~/Scaffold/README.md`.
3. `Templates~/manifest.snippet.json`, `Templates~/link.xml` — 위 설치 계약 참조.

## Editor Exec (번들 — AI 에이전트 Unity 조작)

파운데이션에 **Unity Editor C# 실행 서버 + AI 스킬**이 번들돼 있다(`Editor/Exec/` + `Exec~/`). 별도 패키지 설치 없이 `com.hwi.foundation` 하나로 AI 에이전트(Claude Code 등)가 에디터를 조작·검증할 수 있다.

- **자동 기동:** 에디터를 열면 exec HTTP 서버가 `[InitializeOnLoad]` 로 시작(베이스 포트 8090, 점유 시 폴백). 토큰 인증·화이트리스트·감사 로그 포함.
- **AI 스킬 설치:** 소비 프로젝트 루트에서
  ```bash
  bash Packages/com.hwi.foundation/Exec~/bootstrap.sh "$PWD" --skip-manifest
  ```
  `.claude/skills/unity-editor-ops/` + `CLAUDE.md` 안내 블록 설치. `--skip-manifest` = exec 가 파운데이션에 번들이므로 manifest git 의존 추가 불필요.
- **(1회·필수) Interaction Mode = No Throttling** (`Unity ▸ Settings ▸ General`) — 안 하면 exec 요청이 심하게 지연.
- ⚠ **표준 `com.linestudio.unity-exec` 를 동시에 설치하지 말 것** — asmdef `UnityExec.Editor` 중복 → 컴파일 붕괴. 번들본만 사용.
- 의존: `com.unity.nuget.newtonsoft-json`(package.json 에 선언됨).
- 출처: LINE Studio `unity-exec-cli` @ `51c764b` 를 verbatim vendoring(원본 무수정). 재동기화 절차 → `Documentation~/VENDOR_UNITY_EXEC.md`.

## SpriteRenderer · Orthographic 규약

모든 게임 오브젝트는 `SpriteRenderer`, 좌표/화면 크기는 MainCamera Orthographic Size 기반 동적 대응. 카메라에 `OrthographicCameraFitter` 부착 → 기준 월드 영역을 화면비에 맞춰 자동 fit(Fit/Envelope/FitWidth/FitHeight).

```csharp
using Hwi.Foundation.UI;

var fitter = Camera.main.gameObject.AddComponent<OrthographicCameraFitter>();
fitter.ReferenceWorldSize = new Vector2(10.8f, 19.2f); // 세로 모바일 기준
fitter.Mode = OrthographicCameraFitter.FitMode.Fit;    // 기준 영역 전체 보장
```

## 착수 게이트 (에디터/빌드 필요)

v1.0.0 태깅은 **실 WebGL IL2CPP(High) + 실 Android IL2CPP 왕복 green** 이 전제(에디터 Play/Mono 대체 불가). 단계별 러너 → **`Documentation~/M0_EDITOR_KICKOFF.md`**.

## License

MIT. See `LICENSE`.
