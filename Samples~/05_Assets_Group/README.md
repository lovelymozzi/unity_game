# 05 Assets — Group Release

`AssetGroup` 로 Addressable 자산 3개 (Sprite 2 + Material 1) 를 묶어 일괄 release 하는 데모.

## 동작
- "Load Group" 버튼: `new AssetGroup(releaseOnLowMemory: true)` 안에서 3개 자산 LoadAsync.
- "Release Group" 버튼: `group.Dispose()` → 일괄 release.
- Profiler 의 Texture/Material 메모리가 release 후 줄어드는지 확인.

## 요구
- Addressables 패키지 (`com.unity.addressables` 2.2.2+) 설치
- 본 sample 의 Addressable 자산이 카탈로그에 빌드됨 (`Window/Asset Management/Addressables/Groups → Build Player Content`)

## Keys
- `sample05/icon_a` — Sprite
- `sample05/icon_b` — Sprite
- `sample05/mat_simple` — Material
