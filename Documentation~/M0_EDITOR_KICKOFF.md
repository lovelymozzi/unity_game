# M0 착수 러너 — 에디터/실빌드 게이트

> 이 단계는 **Unity Editor(6000.3.16f1) 기동 + 사내망 도달성 + 실 디바이스 빌드**가 필요하다.
> 문서 작성 시점 세션에서는 에디터 미기동 → 미완. 아래를 순서대로 밟아 v1.0.0 을 태깅한다.
> 배경/근거: `HwiFoundation_현대화_v1.0.0_실행계획.md` (§5·§7·§11·§12·§16).

각 게이트 = **에디터 clean 컴파일 + 해당 기능 실 WebGL/Android IL2CPP 빌드 통과**. 에디터 Play(Mono)는 대체 불가.

## M-1 도달성 검증 (사내망/CI)
- [ ] `https://package.openupm.com` (+ `org.nuget` 업링크) resolve
- [ ] `github.com` — UniTask / Addler / USN / LucidAudio / (NuGetForUnity, v1.0.1)
- [ ] Unity Asset Store — DOTween(계정 게이트, CI restore 불가 → 원자 커밋 우회)
- 막히면: 사내 UPM 미러 or 소스 벤더링(`Assets/` 커밋). 결과로 배포 채널 확정.

## M0 기반 (DLL 반입 전 선행 — 안 하면 팀 에디터/빌드 붕괴)
- [ ] `EXCLUDE_COMPILER_SERVICES_UNSAFE` 스크립팅 define — **전 타깃 그룹**(Standalone/WebGL/**Android/iOS**). AI Assistant 번들 Unsafe(2.11.0-pre.1) 등 4중 충돌 회피. Unity 6000.3.16f1 은 하이재킹 픽스(6000.5.0a2) 이전.
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
`Templates~/Scaffold/*.cs.txt` 의 VERIFY 주석을 실 API 로 교체:
- [ ] R3 provider 심볼(`ObservableSystem.DefaultTimeProvider` / `UnityTimeProvider`), autoReferenced 여부
- [ ] Addler `.DisposeWith()` 네임스페이스 · `AddressablePool`/`Use()`/`Return()`/`BindTo<T>`
- [ ] USN `Screen`/`Page`/`Modal` 라이프사이클 반환형(UniTask 여부) · `ModalContainer` Push/Pop · `AsyncProcessHandle.Task.AsUniTask()`
- [ ] LucidAudio `SetAudioMixerGroup` · `FadeVolume(float,float,Action)` · `Stop(fadeOut)` · `AudioType` full-qualify

## 실빌드 왕복 게이트 (v1.0.0 태깅 전제)
**실 WebGL IL2CPP(High) + 실 Android IL2CPP** 두 빌드 모두 green:
- [ ] R3 — WebGL Subject/Subscribe, TimeProvider(UnityTimeProvider resolve, link.xml 보존). Interval/Delay/ObserveOn/ThreadPool 미사용 확인.
- [ ] LucidAudio — BGM 크로스페이드 청감(무음 버그 소멸 확인), 믹서 그룹 재적용.
- [ ] USN — push/pop + sheet, 뷰 서브클래스 트림 방지(link.xml).
- [ ] DOTween — Pause(timeScale=0) 페이드(`.SetUpdate(true)`), AOT(커스텀 struct tween 금지, float/Color/Vector 만).
- [ ] Addler — 풀 + BindTo 로드, 카탈로그 초기화 후 자동 link.xml 커버.
- [ ] CSV — 경량 `CsvTable` 로드(v1.0.0). CsvCSharp 소스젠 IL2CPP 생존은 v1.0.1 별도 게이트.

## 완료 시
- [ ] `packages-lock.json` 커밋(해시 핀).
- [ ] 실측으로 갱신된 함정/교정을 `claudedocs/` 에 기록(자동 메모리 금지 — 프로젝트 규칙).
- [ ] `git tag hwi-foundation-v1.0.0` (마스터).
