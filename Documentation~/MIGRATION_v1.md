# 마이그레이션 — v0.5 → v1.0.0

## 제거 모듈 → 네이티브 대체

| 제거(v1.0.0 미포함) | 대체 | 이관 패턴 |
|---|---|---|
| `Foundation.Audio` (`AudioChannel`/`BgmController`) | LucidAudio | `new BgmController().CrossfadeAsync(clip, d)` → `LucidAudio.PlayBGM(clip).SetVolume(0).FadeVolume(1, d)` + 이전 BGM `Stop(fadeOut:d)`. namespace `AnnulusGames.LucidTools.Audio`, `AudioType` full-qualify. |
| `Foundation.Events` (`EventChannel`/`EventChannel<T>`/`RuntimeSet`) | R3 | SO 채널 `IntEventChannel.Raise(v)`/`Register(cb)` → `ReactiveProperty<int>` + `.Subscribe(cb).AddTo(...)`. RuntimeSet → `ObservableList`/`Subject`. Producer/Listener 분리는 R3 스트림 소유자 1곳으로. |
| `Foundation.Popup` (`PopupManager`/`Popup`/`Popup<T>`) | USN Modal | 프리팹 = USN Modal 서브클래스. `PopupManager.Open<T>()` → `ModalContainer.Push(key, ...)`. 결과 대기 = `UniTaskCompletionSource` + `AsyncProcessHandle.Task.AsUniTask()`. (스캐폴드 `ConfirmModal`/`ModalHost` 참조.) |
| `FadeAsync` / `FadeTransition` 모션 | DOTween | `FadeAsync.FadeCanvasGroupAsync(cg, a, d)` → `cg.DOFade(a, d).SetEase(Ease.Linear)`. unscaled(Pause/모달) = `.SetUpdate(true)`. `SceneLoader` 의 `ITransition` 은 유지 — DOTween 기반 `ITransition` 구현체를 게임에서 작성. |

## `FadeTransition` 대체 예 (게임측 ITransition)

```csharp
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Hwi.Foundation.Scene;   // ITransition 유지

public sealed class DoFadeTransition : ITransition
{
    public float Duration = 0.3f;
    // EnsureCanvas() 는 구 FadeTransition 과 동일 패턴, 페이드만 DOTween:
    public async UniTask PlayOutAsync(System.Threading.CancellationToken ct)
        => await Canvas.DOFade(1f, Duration).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
    public async UniTask PlayInAsync(System.Threading.CancellationToken ct)
        => await Canvas.DOFade(0f, Duration).SetEase(Ease.Linear).ToUniTask(cancellationToken: ct);
    // ... EnsureCanvas / CanvasGroup 필드
}
```

---

## 실행 위치 & reverse-merge 편차 (기록)

플랜 §4.1 은 v1.0.0 재정비 전에 **임베드 사본의 마스터-대비 앞선 변경(Popup 모듈, `FadeCanvasGroupUnscaledAsync`)을 PKM 마스터로 역병합**하라고 권했다. v1.0.0 실행에서 이를 **물리적으로 수행하지 않았다.** 사유:

1. v1.0.0 은 `Popup` 과 fade 프리미티브를 **명시적으로 제거**한다(결정 4 / §2.3). 역병합 후 즉시 삭제 = churn.
2. 이번 작업은 **커밋 금지** — 역병합의 본래 목적(마스터 git 히스토리 parity)이 성립하지 않음.
3. 임베드 사본(`ShootGameHwi/Assets/Plugins/HwiFoundation`)은 **v0.5 동결·미변경**으로 보존 → 앞선 작업이 유실되지 않음. 그 자체가 **canonical 이관 소스**다.

### canonical 소스 위치
- **Popup 모듈 원본**: `ShootGameHwi/Assets/Plugins/HwiFoundation/Runtime/Popup/` (`Popup.cs`/`PopupManager.cs`/`PopupOfT.cs` + `Foundation.Popup.asmdef`). → §13 ShootGame 이관 시 USN Modal 로 변환.
- **`FadeCanvasGroupUnscaledAsync`**: 임베드 사본 `Runtime/Async/FadeAsync.cs` (마스터엔 없던 unscaled 변형). → DOTween `.SetUpdate(true)` 로 대체.

> 다른 방식(임베드에 deprecated 심 유지 후 점진 제거, 또는 실제 역병합 후 삭제)을 원하면 이 편차를 되돌릴 수 있음.

---

## 후속(deferred) — 별도 승인

- **ShootGame 소급 이관(결정 6):** 임베드 사본을 v1.0.0 으로 승격하며 `Foundation.Events` 75곳/16파일 → R3, `Foundation.Popup` 18파일 + 프리팹6 + 에디터빌더5 → USN Modal, 오디오/모션 → LucidAudio/DOTween. 대규모 breaking → 단독 PR·단독 승인.
- **타 게임(결정 7):** MergeLegion/MergeArsenal 은 파운데이션 v1.0.0 확정 후 게임별 판단(현재 파운데이션 미참조).
- **v1.0.1:** CsvCSharp + NuGetForUnity 재도입(Unsafe 충돌 선결 + 두 실빌드 왕복 green 후).
