# HWI Foundation — 4계층 스캐폴드

신규 프로젝트 시작용 계층별 시작 코드. 프로젝트의 `Assets/Scripts/` 아래로 복사하고 `.cs.txt` → `.cs` 로 확장자를 바꾼다.

## 계층 & 파일

| 계층 | 파일 | 네임스페이스 | 사용 패키지 |
|---|---|---|---|
| **DataManager** | `CsvTable.cs.txt`, `DataManager.cs.txt`, `UnitStats.csv` | `Game.Data` | 경량 CSV(v1.0.0)→CsvCSharp(v1.0.1) · Addressables · Addler · UniTask |
| **Core/Manager** | `FoundationBootstrap.cs.txt`, `GameManager.cs.txt` | `Game.Core` | R3 · Addler · UniTask · Addressables · Foundation(Core/Mobile/Logging) |
| **Controller/Object** | `EnemyActor.cs.txt` | `Game.Actors` | SpriteRenderer · DOTween · `UnityEngine.Pool` · UniTask |
| **UI/View** | `MainScreen.cs.txt`, `ConfirmModal.cs.txt`, `ModalHost.cs.txt` | `Game.UI` | Unity Screen Navigator · TMP · DOTween · R3 |

## 데이터 흐름

```
CSV (Addressable TextAsset)
   └─ DataManager.LoadAllAsync (UniTask, 부팅 시 1회, Addler 수명 바인딩)
        └─ GameManager (R3 ReactiveProperty 상태 소유)
             ├─ EnemyActor (DOTween 연출 + UnityEngine.Pool)
             └─ MainScreen/ConfirmModal (USN + TMP, R3 구독 = 단방향)
```

## ⚠ VERIFY 주석

`⚠ VERIFY` 가 붙은 줄은 라이브러리 API명/시그니처가 **에디터 실측 전**(플랜 §12/§16.3)이라는 표시다.
M0 에디터 기동(6000.3.16f1) + 5종 라이브러리 설치 후 실제 API 로 확정한다:

- **R3**: provider 심볼(`ObservableSystem.DefaultTimeProvider`), autoReferenced 여부
- **Addler**: `.DisposeWith()` 네임스페이스, 어셈블리명 `Addler`
- **DOTween**: 생성 asmdef `DOTween.Modules` 참조
- **USN v1.7.5**: `Screen`/`Page`/`Modal` 라이프사이클 반환형, `ModalContainer` Push/Pop, `AsyncProcessHandle.Task.AsUniTask()`
- **LucidAudio**: `AnnulusGames.LucidTools.Audio`, 믹서 그룹 재적용

각 소비 asmdef 는 `Foundation.*` + `R3`/`R3.Unity`/`DOTween.Modules`/`Addler`/`UnityScreenNavigator`/`AnnulusGames.LucidAudio.Runtime` 를 **명시 참조**해야 한다(26 named asmdef 는 autoReferenced 상속 안 함 — §16.3).
