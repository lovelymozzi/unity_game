# M0 착수 러너 — 에디터/실빌드 게이트

> 이 단계는 **Unity Editor(6000.3.16f1) 기동 + 사내망 도달성 + 실 디바이스 빌드**가 필요하다.
> 문서 작성 시점 세션에서는 에디터 미기동 → 미완. 아래를 순서대로 밟아 v1.0.0 을 태깅한다.
> 배경/근거: `HwiFoundation_현대화_v1.0.0_실행계획.md` (§5·§7·§11·§12·§16).

각 게이트 = **에디터 clean 컴파일 + 해당 기능 실 WebGL/Android IL2CPP 빌드 통과**. 에디터 Play(Mono)는 대체 불가.

---

## ✅ 실측 검증 완료 (2026-07-08, canonical repo `TestProject~/`, unity-exec)

Unity 6000.3.16f1 테스트 프로젝트(`file:../../` 로 패키지 로컬 참조)에서 아래까지 **에디터 컴파일 green** 확인:
- **패키지 해석 + 5종 라이브러리 + 11 foundation asmdef 컴파일 green** (errorCount 0). R3 1.3.1(+org.nuget.r3)·Addler·USN 1.7.5·LucidAudio 1.0.1·UniTask 2.5.11 + DOTween(공식 Demigiant 6000.3 소스).
- **스캐폴드 8개 컴파일 green** (errors 0, warnings 0) — 아래 실측 교정 반영 후.

**🔑 실측 교정 (이전 추측 대비 — 반드시 반영):**
- **`EXCLUDE_COMPILER_SERVICES_UNSAFE` 는 클린 신규 프로젝트엔 불필요** — Unsafe 단 1벌(중복 없음). ai.assistant/burst/collections 가 있는 **ShootGame 임베드에서만** 필요.
- **define `USN_USE_ASYNC_METHODS` 필수** — 없으면 USN 라이프사이클이 `IEnumerator`(코루틴). 있으면 `Task` 반환 → UniTask await 브리지 가능. (UniTask 아님, `System.Threading.Tasks.Task`.)
- **define `UNITASK_DOTWEEN_SUPPORT` 필수** — DOTween 이 raw DLL 이라 UniTask versionDefine 자동감지 불가 → `Tween.ToUniTask()` 확장을 수동 define 로 켜야 함. **‼ 순서 하드 의존:** 이 define 는 **DOTween 을 실제 임포트한 뒤에만** 켠다. DOTween 없이 켜면 UniTask 의 `DOTweenAsyncExtensions.cs`(`#if UNITASK_DOTWEEN_SUPPORT`, `DG`/`Tween`/`TweenCallback` 참조)가 CS0246 로 컴파일 실패 → **스크립트 로드 붕괴로 에디터가 아예 안 열림**. → §M0 체크리스트에서 이 항목을 DOTween 임포트 뒤로 둘 것.
- **DOTween 코어 어셈블리 = `DOTween`** (‼ `DOTween.Modules` 아님). 모듈 소스는 firstpass(플러그인 DLL 방식) → link.xml 은 `DOTween` preserve.
- **Addler 수명바인딩 = `handle.BindTo(gameObject)`** (‼ `DisposeWith` 아님), ns `Addler.Runtime.Core.LifetimeBinding`, 어셈블리 `Addler`. 반환=핸들(체인).
- **USN: 베이스 `Page`/`Modal`**(‼ `Screen`/`Screens`/`Modals` 아님, ns 단수 `...Core.Page`/`...Core.Modal`). `ModalContainer.Find(string)`, `Push(key,playAnimation,modalId=null,loadAsync=true,onLoad)`, onLoad=`(string modalId, Modal modal)` 튜플, `AsyncProcessHandle.Task`(→`.AsUniTask()`).
- **R3**: `ObservableSystem.DefaultTimeProvider`(프로퍼티) + `UnityTimeProvider`/`UnityFrameProvider`(R3.Unity) 실측.

**미완 = 실 WebGL IL2CPP(High) + 실 Android IL2CPP 왕복 빌드**(에디터 컴파일로 대체 불가). 빌드 타깃은 Unity API 상 Android/WebGL/iOS 지원 확인됨.

## M-1 도달성 검증 (사내망/CI)
- [ ] `https://package.openupm.com` (+ `org.nuget` 업링크) resolve
- [ ] `github.com` — UniTask / Addler / USN / LucidAudio / (NuGetForUnity, v1.0.1)
- [ ] Unity Asset Store — DOTween(계정 게이트, CI restore 불가 → 원자 커밋 우회)
- 막히면: 사내 UPM 미러 or 소스 벤더링(`Assets/` 커밋). 결과로 배포 채널 확정.

## M0 기반 (DLL 반입 전 선행 — 안 하면 팀 에디터/빌드 붕괴)
- [x] ~~`EXCLUDE_COMPILER_SERVICES_UNSAFE`~~ — **클린 신규 프로젝트엔 불필요**(2026-07-08 실측: Unsafe 1벌). ShootGame 임베드(ai.assistant/burst/collections)에서만 필요.
- [ ] **define `USN_USE_ASYNC_METHODS`** — 전 타깃 그룹. USN 라이프사이클 Task 화(미설정=IEnumerator). ‼ 필수.
- [ ] **define `UNITASK_DOTWEEN_SUPPORT`** — 전 타깃 그룹. DOTween(raw DLL) ↔ UniTask `.ToUniTask()` 브리지. ‼ 필수. **‼ DOTween 임포트(§아래 `- [ ] DOTween: Asset Store ...`)를 먼저 끝낸 뒤에 켤 것** — 순서 어기면 CS0246 로 에디터가 안 열린다(트러블슈팅 참고).
- [ ] Assembly Version Validation = OFF (Player Settings, 커밋).
- [ ] WebGL managedStrippingLevel = High(3), `stripEngineCode=1` 확인. link.xml 의 "Low" 주석 → "High(3)" 정정.
- [ ] `link.xml` **append**(신규 생성/덮어쓰기 금지) — `Templates~/link.xml` 항목 추가.
- [ ] `versionDefine` 실동작 확인 — `HWI_INGAME_CONSOLE` 는 이 프로젝트에서 정의된 적 없음(선례 신뢰 말 것). UniTask git-pin versionDefines 발화도 실측.

## 라이브러리 설치 & 컴파일 green (manifest.snippet.json)
- [ ] R3: `com.cysharp.r3` + `org.nuget.r3` **둘 다**(core-only 금지). TimeProvider 정확 핀(≥8.0.0, OpenUPM 최신 10.0.9 재라우팅 주의).
- [ ] Addler git-URL `#1.0.1`(OpenUPM 는 조용히 1.0.0). 어셈블리명 `Addler` 확인.
- [ ] USN git-URL `#v1.7.5`. `USN_USE_ADDRESSABLES` 는 Addressables 2.9.1 존재로 import 즉시 ON → 첫파도 컴파일 리스크 주시.
- [ ] LucidAudio git-URL `?path=/Assets/LucidAudio#v1.0.1`. namespace `AnnulusGames.LucidTools.Audio`, 어셈블리 `AnnulusGames.LucidAudio.Runtime`.
- [ ] DOTween: Asset Store free core → Setup → **Create ASMDEF `DOTween.Modules`** → 소스+asmdef+DOTweenSettings.asset+.meta **원자 커밋**.
- [ ] 26 named asmdef 는 autoReferenced 상속 안 함 → R3.Unity/DOTween.Modules/Addler/USN/LucidAudio 를 **소비 asmdef 마다 명시 참조**.
- [ ] `AddressableAssetSettings` + 최소 1그룹 + 주소 지정(없으면 Addler 진입점 전부 `InvalidKeyException`).

## 스캐폴드 `⚠ VERIFY` 실측 확정
`Templates~/Scaffold/*.cs.txt` 의 VERIFY 주석을 실 API 로 교체 — **완료(2026-07-08, 컴파일 green)**:
- [x] R3 provider 심볼 = `ObservableSystem.DefaultTimeProvider` + `UnityTimeProvider`/`UnityFrameProvider`(R3.Unity). ✅
- [x] Addler = `handle.BindTo(gameObject)`(‼ DisposeWith 아님), ns `Addler.Runtime.Core.LifetimeBinding`, 반환=핸들 체인. ✅
- [x] USN 베이스 `Page`/`Modal`(단수 ns), 라이프사이클 `Task`(USN_USE_ASYNC_METHODS), `ModalContainer.Find/Push/Pop`, `AsyncProcessHandle.Task.AsUniTask()`. ✅
- [ ] LucidAudio `SetAudioMixerGroup`·`FadeVolume`·`AudioType` full-qualify — **스캐폴드에 LucidAudio 사용처 없음**(FoundationBootstrap 는 믹서 그룹 캐싱만). 오디오 실사용 시 별도 검증(실빌드 크로스페이드 청감 게이트에서).
- DOTween 어셈블리 `DOTween`, `Tween.ToUniTask()`(UNITASK_DOTWEEN_SUPPORT). ✅

## 실빌드 왕복 게이트 (v1.0.0 태깅 전제)
**실 WebGL IL2CPP(High) + 실 Android IL2CPP** 두 빌드 모두 green:
- [ ] R3 — WebGL Subject/Subscribe, TimeProvider(UnityTimeProvider resolve, link.xml 보존). Interval/Delay/ObserveOn/ThreadPool 미사용 확인.
- [ ] LucidAudio — BGM 크로스페이드 청감(무음 버그 소멸 확인), 믹서 그룹 재적용.
- [ ] USN — push/pop + sheet, 뷰 서브클래스 트림 방지(link.xml).
- [ ] DOTween — Pause(timeScale=0) 페이드(`.SetUpdate(true)`), AOT(커스텀 struct tween 금지, float/Color/Vector 만).
- [ ] Addler — 풀 + BindTo 로드, 카탈로그 초기화 후 자동 link.xml 커버.
- [ ] CSV — 경량 `CsvTable` 로드(v1.0.0). CsvCSharp 소스젠 IL2CPP 생존은 v1.0.1 별도 게이트.

## 트러블슈팅
- **에디터가 실행 직후 에러/컴파일 실패로 안 열림 + `error CS0246: ... 'DG' / 'Tween' / 'TweenCallback'` (`UniTask/.../External/DOTween/DOTweenAsyncExtensions.cs`)**
  → **원인:** `UNITASK_DOTWEEN_SUPPORT` define 가 켜져 있는데 DOTween 이 아직 임포트되지 않음(순서 역전). 그 파일은 `#if UNITASK_DOTWEEN_SUPPORT` 로만 감싸져 있어 define 만 있으면 DOTween 타입을 찾다 실패 → 전체 스크립트 로드 붕괴.
  → **즉시 복구:** 에디터 종료 상태에서 `ProjectSettings/ProjectSettings.asset` 의 `scriptingDefineSymbols` 4개 타깃(Android/Standalone/WebGL/iPhone)에서 `UNITASK_DOTWEEN_SUPPORT` 제거(`USN_USE_ASYNC_METHODS` 는 유지) → 재실행. `Unity -batchmode -quit -nographics -projectPath <경로>` ExitCode 0 로 검증 가능.
  → **정상화:** DOTween 임포트(README §2) 완료 후 4개 타깃에 `UNITASK_DOTWEEN_SUPPORT` 재추가.
  → **재발 방지:** define 은 **DOTween 임포트 뒤에만** 켠다(위 §M0 체크리스트 순서).

## 완료 시
- [ ] `packages-lock.json` 커밋(해시 핀).
- [ ] 실측으로 갱신된 함정/교정을 `claudedocs/` 에 기록(자동 메모리 금지 — 프로젝트 규칙).
- [ ] `git tag v1.0.0` + `git push origin v1.0.0` (canonical repo `hwi-foundation`, remote `phj9033/Unity_Foundation_JG`).
