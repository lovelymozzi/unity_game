# Changelog

All notable changes to this package will be documented here. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), versioning is [SemVer](https://semver.org/).

## [1.0.0] — 2026-07-08

thin adopt/bootstrap 파운데이션 기준선. 모듈 제거 + 역할 전환 = **대규모 breaking → v1.0.0 승격**.
자세한 배경/게이트: `HwiFoundation_현대화_v1.0.0_실행계획.md`.

### Changed (**Breaking**)
- 파운데이션 역할 재정의: **얕은 채택 + 부트스트랩 + 컨벤션**. 게임 코드는 라이브러리 네이티브 API 직접 사용, 중복 래퍼 폐기.
- 표준 스택 하드 채택: R3(이벤트/반응형) · Addler(에셋 수명) · DOTween(모션) · Unity Screen Navigator(화면/모달) · LucidAudio(오디오).
- `package.json` 의존 floor 갱신: `com.unity.addressables` 2.2.2 → 2.9.1, `com.unity.inputsystem` 1.7.0 → 1.19.0.

### Removed
- **`Foundation.Audio`** (`AudioChannel`/`BgmController`) → LucidAudio 직접. (마스터 소비자 0 — 즉시 안전 제거. 크로스페이드 무음 버그 클래스 소멸.)
- **`Foundation.Events`** (`EventChannel`/`EventChannel<T>`/`RuntimeSet`) → R3 `Observable`/`ReactiveProperty`/`Subject` 직접.
- **`Foundation.Async.FadeAsync`** + **`Foundation.Scene.FadeTransition`** 모션 프리미티브 → DOTween 직접. (`Foundation.Scene` 은 `SceneLoader`/`ITransition`/lifecycle hook 유지, asmdef 에서 `Foundation.Async`·`Foundation.UI` 참조 제거.)
- Samples `03_Events_Counter`, `04_Scene_Transition` — 제거 모듈 의존이라 함께 삭제. (`01_Pool`/`02_UI_SafeArea`/`05_Assets` 유지.)
- `Tests/EditMode` + `Tests/PlayMode` 전수 삭제(v0.5 Unreleased 이월) — 배포 트리 단순화. 누적 92건(EditMode 50 + PlayMode 42) 회귀 안전망 손실, tag `hwi-foundation-v0.5.0` commit `7c55215` 복원 가능. `Runtime/Mobile/AssemblyInfo.cs`·`Runtime/Assets/AssemblyInfo.cs`(Tests 전용 `[InternalsVisibleTo]`) 동반 제거.

### Added
- **`Foundation.UI.OrthographicCameraFitter`** — orthographic `orthographicSize` 를 기준 월드 영역 + 화면비로 동적 fit(Fit/Envelope/FitWidth/FitHeight). SpriteRenderer/Orthographic 표준의 월드판(§14).
- **`Templates~/`** (Unity-ignored, copy-on-install):
  - `ProjectConventions.md` — 개발 환경·기술 스택·코드 컨벤션·출력 요구사항 단일 출처(신규 프로젝트 CLAUDE.md 로 복사).
  - `Scaffold/` — 4계층 시작 코드(`CsvTable`/`DataManager`/`FoundationBootstrap`/`GameManager`/`EnemyActor`/`MainScreen`/`ConfirmModal`/`ModalHost`) + 샘플 CSV. `⚠ VERIFY` 주석 = 라이브러리 API 실측 전.
  - `manifest.snippet.json` — scopedRegistry(R3 OpenUPM) + git-URL/UPM 의존 recipe.
  - `link.xml` — WebGL/IL2CPP preserve 템플릿(append 규약).
- **`Documentation~/M0_EDITOR_KICKOFF.md`** — 에디터/실빌드 착수 러너, **`MIGRATION_v1.md`** — 제거 모듈 이관 + ShootGame 소급(후속).

### Notes / 게이트
- README 를 **설치 계약**으로 재작성(scopedRegistry·git-URL·`EXCLUDE_COMPILER_SERVICES_UNSAFE`·Assembly Version Validation OFF·link.xml·NuGet 정책). package.json 이 표현 못 하는 opt-in 은 이 문서가 유일 계약.
- **CsvCSharp + NuGetForUnity 는 v1.0.1 로 이월** — Unsafe 4중충돌 선결·소스젠 AOT/WebGL 미증명·CSV 사용처 0. v1.0.0 은 경량 `CsvTable` 리더.
- **v1.0.0 태깅은 실 WebGL IL2CPP(High) + 실 Android IL2CPP 왕복 green 전제**(에디터 Play/Mono 대체 불가). 본 릴리스 산출물은 코드/구조/문서/템플릿 = 에디터 비의존분. 라이브러리 설치·컴파일·실빌드 검증은 M0 착수 게이트(미완).

## [0.5.0] — 2026-05-22

### Added
- **Foundation.Save**: `ISaveStore` 인터페이스 + `PlayerPrefsSaveStore` (JsonUtility wrap) + `JsonFileSaveStore` (persistentDataPath, 원자적 tmp→rename) + `SaveContext.Default` static facade.
- **Foundation.Audio**: `AudioChannel` (Foundation.Pool.PrefabPool 재사용한 AudioSource 풀, clip.length 후 자동 release) + `BgmController` (CrossfadeAsync replace 정책, 두 채널 alternation 무한 반복).
- **Foundation.Localization**: `LocalizationTable` ScriptableObject + `Locale` static (RegisterTable / UnregisterTable / SetLocale / Get + `LocaleChanged` 이벤트).
- **Foundation.Input**: `ActionMapBinder` (InputActionAsset wrap, additive Bind, Dispose 시 일괄 unsubscribe).
- **Foundation.Assets**: `AssetKeys.IsRegistered(key)` — Addressables `IResourceLocator.Locate` 동기 조회 헬퍼.
- 테스트: EditMode 50/50 (v0.4 43 → +7: Save 3 + Localization 2 + Input 1 + AssetKeys 1), PlayMode 42/42 (v0.4 41 → +1: Audio). 전수 PASS.
- 외부 의존 +1: `com.unity.inputsystem` 1.7.0+

### Notes
- Breaking 0 — v0.5 는 순수 추가형.
- 4 신규 모듈 모두 sample 없음 — v0.6+ 안정화 단계 추가 검토.
- Cleanup §3.2 (scratch HookProbe master 이관) 는 v0.6 deferral — scratch SceneB.unity 의 m_Script GUID swap 이 필요해 본 v0.5 scope 밖.

## [0.4.0] — 2026-05-22

### Added
- **Foundation.Assets**: Addressables 래퍼. `AssetHandle<T>` (static async 팩토리 + idempotent Dispose), `AssetGroup` (LoadAsync<T> + Adopt + 일괄 Dispose + `releaseOnLowMemory` opt-in 으로 Mobile.LowMemory 자동 연동).
- **Foundation.Mobile.DeviceTier**: `SystemInfo.systemMemorySize` 기반 Low/Mid/High 분기 헬퍼. 임계 `<2048MB / <4096MB / ≥4096MB` (strict less-than). 테스트용 `Override` 노출.
- **Sample 05**: Assets — Group Release (UI 버튼 두 개로 그룹 로드·해제, `releaseOnLowMemory=true`).
- **Tests**: EditMode 43 (v0.3 36 → +7, DeviceTier Override 1 + Compute 경계 4 + AssetHandle idempotent Dispose 1 + 1 carryover), PlayMode 41 (v0.3 35 → +6, AssetGroup LoadAsync 성공·실패 2 + Adopt no-op 1 + releaseOnLowMemory true/false 2 + HookProbe 격리 1). 전수 PASS.
- `com.unity.addressables` 2.2.2+ 의존 추가.

### Changed (**Breaking**)
- `Hwi.Foundation.Scene.IBeforeSceneActive` → `IOnSceneReady` rename.
  - 인터페이스명·메서드명·파일명 모두 변경: `OnBeforeSceneActiveAsync` → `OnSceneReadyAsync`.
  - 의미는 v0.3 와 동일 ("씬 활성화 직후, 사용자 visible 직전"), 이름이 실제 동작과 일치하도록 정정.

### Internal
- `Foundation.Assets` / `Foundation.Mobile` 모두 `[InternalsVisibleTo]` 로 테스트 어셈블리에 internal 노출 (`OwnedByGroup`, `Compute`, `DisposeInternal`, `CreateForTest`).
- Subagent scope exception protocol 명문화 (spec §9).

## [0.3.0] — 2026-05-22
### Added
- **Foundation.Scene**: `SceneLoader.LoadAsync` (Single + transition + progress), `LoadAdditiveAsync`, `UnloadAsync` (idempotent). `ITransition` + `FadeTransition` 빌트인. `IBeforeSceneActive` / `IAfterSceneLoaded` 라이프사이클 hook (씬 안 자동 탐색).
- **Foundation.Logging**: `UnityDebugFoundationLogger` (기본, Debug.* routing + tag prefix), `IngameDebugConsoleAdapter` (OSS optional, `asmdef versionDefines` → `HWI_INGAME_CONSOLE` 게이팅).
- **Samples**: `04_Scene_Transition` (Fade A↔B 데모). `03_Events_Counter` 자산 backfill (씬 + CounterSO.asset).
- **Tests**: EditMode +6 (UnityDebugLogger 3 + FadeTransition config 1 + SceneLoader args 2), PlayMode +10 (SceneLoader 5 + LoadAdditive/Unload 2 + FadeTransition 2 + SceneHook 1).

### Changed
- **Foundation.Scene**: `IBeforeSceneActive` / `IAfterSceneLoaded` 둘 다 활성화 이후에 실행 (Unity 의 `allowSceneActivation=false` 상태에서 `GetRootGameObjects()` 가 빈 배열을 반환하는 동작 때문). 의미는 "씬 visible 직전 / 직후" 로 재해석. v0.4 에서 interface 이름 검토 예정.

### Fixed
- (scratch tool) HwiTestRunner EditMode 더블카운트 — `[InitializeOnLoad]` `SessionState` 가드로 1회 등록 보장.

### Notes
- 외부 의존 추가 0. UniTask 만 유지.
- OSS adapter 는 manifest 에 `com.yasirkula.ingamedebugconsole` 추가 시 자동 활성. 미설치 시 코드 영역 컴파일에서 빠짐.

## [0.2.0] — 2026-05-22
### Added
- **Foundation.Async** (UniTask 의존 진입): `AwaitableInterop.AsUniTask` (Unity 6 Awaitable ↔ UniTask), `UniTaskExtensions.ForgetWithLog`, `MonoBehaviourExtensions.GetCancellationTokenOnDestroy`, `DelayAsync.DelayFramesAsync`, `FadeAsync.FadeCanvasGroupAsync`
- **Foundation.Events**: `EventChannel` (no-arg), `EventChannel<T>` + `IntEventChannel`/`FloatEventChannel`/`StringEventChannel` 구체 SO, `RuntimeSet<T>`
- **Foundation.Pool.PrefabPoolGroup**: `Unregister(key)` — v0.1 빚 청산 #2
- **Foundation.Pool.PrefabPool**: active instance 트래킹 — `Dispose()` 가 활성/비활성 instance 모두 destroy
- **Samples**: `03_Events_Counter` (IntEventChannel 데모 — CounterButton + CounterDisplay)
- **Tests**: EditMode +20 (PoolGroup Contains 8 + EventChannel 6 + EventChannel&lt;T&gt; 4 + AwaitableInterop 1 + UniTaskExtensions 1), PlayMode +13 (Unregister 3 + RuntimeSet 5 + MB ext 1 + DelayAsync 2 + FadeAsync 2)
- Test asmdef 의존성 정리 — EditMode/PlayMode 가 Foundation.Pool/Events/Async + UniTask 참조

### Notes
- **외부 의존 +1**: UniTask 2.5.x+ (Cysharp git URL). Async 모듈에 격리됨 — Pool/UI/Mobile/Core/Events 는 UniTask 없이도 동작.
- AwaitableInterop 의 null 가드는 동기 throw 보장 위해 entry-point(non-async) + inner(async) 패턴.
- UniTaskExtensions.ForgetWithLog 는 async void try/catch wrap (UniTask 2.5 에 `Forget(Action&lt;Exception&gt;)` 없음).

## [0.1.0] — 2026-05-22
### Added
- Foundation.Core: IService, IInitializable, IDisposableHandle, Result<T>, Option<T>, IFoundationLogger
- Foundation.Pool: PrefabPool (`Prewarm(int count)` 포함), PrefabPoolGroup (UnityEngine.Pool 위 wrapping)
- Foundation.UI: CanvasScalerPreset, SafeAreaFitter, LetterboxController
- Foundation.Mobile: MobileBootstrap, LowMemoryDispatcher, ThermalMonitor
- Samples: 01_Pool_Bullet (Bullet2D.prefab + PoolBulletScene), 02_UI_SafeArea (SafeAreaScene)
- Tests: EditMode 10 + PlayMode 12 = 22 통과 (Unity 6000.0.59f2)
