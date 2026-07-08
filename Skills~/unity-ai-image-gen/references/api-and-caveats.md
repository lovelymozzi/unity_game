# Unity AI Generators — internal API 표면 & 함정

`AiGenProbe` 가 리플렉션으로 호출하는 대상. 어셈블리 `Unity.AI.Generators.Tools`(assistant 패키지 내장), 전부 `internal`.

## 진입점

```text
GenerationHandle<UnityEngine.Object> AssetGenerators.GenerateAsync<TSettings>(
        GenerationParameters<TSettings> parameters, CancellationToken ct);
Task<IReadOnlyList<ModelInfo>> AssetGenerators.GetAvailableModelsAsync(bool includeAllModels, CancellationToken ct);
// 부가: GenerateSpritesheetAsync, RemoveSpriteBackgroundAsync, UpscaleImageAsync, RecolorImageAsync, QuoteAsync ...
```

## 타입 멤버 (필드/프로퍼티 명이 리플렉션 키)

- **`GenerationParameters<TSettings>`** (fields): `Prompt`(string), `ModelId`(string), `SavePath`(string), `AssetType`(Type), `Settings`(TSettings), `TargetAsset`(Object), `PermissionCheckAsync`(`Func<string,long,Task>` — 포인트 소모 승인 콜백; `Task.CompletedTask` 반환 시 자동 승인).
- **`SpriteSettings`** (fields): `Width`(int), `Height`(int), `Loop`(bool), `RemoveBackground`(bool), `ImageReferences`(`ObjectReference[]`). ※ `Width/Height` 는 생성 해상도를 제어하지 못함(모델 고정 버킷, 보통 1024²). 크기는 후처리 `Resize` 로.
- **`ObjectReference`** (fields): `Image`(UnityEngine.Object — 참고이미지), `Label`(string). 현재 `ImageReferences[0]` 만 사용됨.
- **`ModelInfo`** (fields): `ModelId`(string), `Description`(string). Description 에 `Modalities: ..., SupportsSprites, SupportsImageReference` 등이 들어있다.
- **`GenerationHandle<T>`** (properties): `Placeholder`(T), `GenerationTask`(`Task<T>`), `DownloadTask`(`Task<T>`), `ValidationTask`(Task), `PointCost`(long), `Messages`(IReadOnlyList<string>).

## 접근성

`AssetGenerators` 는 `internal`. `InternalsVisibleTo` 는 `Unity.AI.Assistant.Editor`, `Unity.AI.Assistant.AssetGenerators.Editor`, `Unity.AI.Toolkit.Tests` 에만. → 프로젝트 스크립트 직접 호출 시 CS0122 → **리플렉션 필수**.

## 모델 (검증 시점 63개 중 sprite+imageref 발췌)

```
범용:  gpt-image-1-5, gpt-image-1, gemini-3.0-pro, gemini-3.1-flash, flux-2-pro, flux-2-dev, seedream-4-5
화풍:  anime-fantasy-characters-2, dark-anime-2, colorful-digital-icons, card-frames,
       game-ui-elements-flux, game-ui-essentials-2, rpg-environment, stylized-3d
후처리: gpt-image-1-5-recolor, scenario-image-transform, magnific-upscaler-precision,
       photoroom-bg-removal, scenario-upscale-v3
```
전체·최신 목록은 `uai.sh models` 로 조회.

## unity-exec 비자명 동작 (하네스가 이 구조를 쓰는 이유)

1. exec 코드는 method body — statement + `return expr;`. 식만 나열하면 파싱 에러.
2. **`Task` 를 반환해도 await 하지 않고 직렬화만 함** → 비동기는 fire-and-forget + `EditorPrefs` 폴링.
3. **`autoReferenced:false` 어셈블리(`AiGenProbe.Editor`) 타입은 exec 스니펫에서 안 보임** → `AppDomain...GetTypes()` 로 찾아 리플렉션 Invoke.
4. 셸에서 파일 mv/cp 후 `AssetDatabase.Refresh(ForceSynchronousImport)` → 재컴파일해야 새 스크립트가 인식됨.
5. 인자(프롬프트·경로)는 base64 로 넘겨 C# 리터럴 이스케이프 문제를 회피(`uai.sh` 가 처리).

## 패키지 함정

`com.unity.ai.assistant` 가 `Unity.AI.Generators.*` / `Unity.AI.Toolkit.*` 를 임베드. `com.unity.ai.generators` 를 따로 manifest 에 넣으면 `Assembly with name '...' already exists` + GUID 충돌로 컴파일 붕괴. **별도 설치 금지.**
