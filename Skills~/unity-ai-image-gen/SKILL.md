---
name: unity-ai-image-gen
description: >-
  Unity AI(내장 Generators)로 프로젝트 안에 이미지/스프라이트/아이콘/텍스처/컨셉아트를 코드로 생성한다.
  Claude 가 실행 중인 Unity 에디터를 공식 Unity CLI 로 구동해 자동 생성·저장한다.
  사용자가 "이미지 만들어줘", "스프라이트 생성", "아이콘 뽑아줘", "버블/캐릭터 아트 만들어",
  "이 이미지 참고해서 비슷하게", "플레이스홀더 아트 필요해", "generate a sprite/icon/texture",
  "concept art" 등 에셋 이미지를 만들고자 하면 — 명시적으로 'Unity AI' 를 말하지 않아도 — 이 스킬을 사용한다.
  특히 기존 프로젝트 이미지를 참고(reference)하거나 기존 에셋 크기에 맞춰야 할 때 적합하다.
  (코드/로직 작성, 기존 이미지 단순 편집·리네이밍, 이미지 읽기/분석만 하는 요청에는 쓰지 않는다.)
compatibility: >-
  공식 Unity CLI(`unity` 바이너리 + com.unity.pipeline 패키지) 필요.
  scripts/uai.sh 는 자체 CLI 전송(`unity command eval`)을 내장하며 프로젝트 루트 기준으로 동작한다.
  구동 하네스 AiGenProbe 는 com.hwi.foundation 패키지 Editor/AiGenProbe/ 에 번들됨(별도 설치 불필요).
  공통 CLI 사용법·규약은 unity-cli 스킬 참고. playtest 스킬과 동일하게 CLI 위에서 독립적으로 공존하는 별도 스킬이다.
---

# unity-ai-image-gen

실행 중인 Unity 에디터의 **내장 Unity AI Generators**(`Unity.AI.Generators.Tools.AssetGenerators`)를 통해
텍스트 프롬프트(+선택적 참고 이미지)로 스프라이트/이미지를 생성하고 프로젝트 에셋으로 저장한다.
해당 API 는 Unity 비공개 `internal` 이라, 번들된 에디터 하네스(`AiGenProbe`)를 **리플렉션**으로 호출해 우회한다.

모든 조작은 `scripts/uai.sh` 한 진입점으로 한다. Claude 가 리플렉션 스니펫을 직접 짜지 말고 이 스크립트를 쓴다.

## 전제 조건 (사용자 환경)

- Unity 6.2+ (검증: 6.3 / `com.unity.ai.assistant`).
- **`com.unity.ai.generators` 패키지를 따로 설치하지 말 것** — assistant 가 Generators 를 내장하므로 별도 설치 시 어셈블리 중복으로 컴파일이 깨진다.
- 1회성 수동 설정(코드로 불가): ① AI 메뉴 약관 동의 ② 프로젝트를 Unity Cloud 프로젝트에 링크 ③ 포인트(크레딧) 잔량.
- Unity 에디터 + Pipeline 서버가 떠 있어야 한다(확인 `unity status`). 없으면 콜드스타트 `unity open <projectPath>` 안내.

## 비용 확인 정책 (운영 규칙)

생성은 **포인트(실제 비용)를 소모**한다. 컨텐츠 개발 중 이미지가 필요해지면 **작업 단위로 묶어 사전 일괄 확인 후 자동 실행**한다:

1. 그 작업에 필요한 이미지를 먼저 전부 식별 → **목록 + 총 매수 + 예상 cost** 를 한 번에 노티
2. 사용자 OK 1회 받으면
3. 나머지(생성 → 스프라이트 임포트 → resize → prefab `[SerializeField]` 와이어링)는 **무확인 자동 실행**
4. 작업 중 예상 못 한 추가 이미지가 필요해지면 그때 다시 총량 노티만 한다

단발성 1장 요청은 그 1장 cost만 알리고 진행한다. 매 생성마다 끊지 않되, 비용은 작업 시작 시점에 한 번 통제한다.

## 워크플로우

프롬프트가 모호하거나 덮어쓰기가 우려되면 먼저 사용자에게 확인한다.

1. **하네스 확인**
   ```bash
   .claude/skills/unity-ai-image-gen/scripts/uai.sh ensure
   ```
   AiGenProbe 는 `com.hwi.foundation` 패키지 `Editor/AiGenProbe/` 에 **번들**돼 있어 별도 설치가 필요 없다. `ensure` 는 로드 여부만 확인한다(미로드면 `com.unity.2d.sprite` 설치/컴파일 에러 점검).

2. **(선택) 모델 고르기** — 화풍/모달리티 확인이 필요할 때만.
   ```bash
   .claude/skills/unity-ai-image-gen/scripts/uai.sh models "SupportsSprites"
   ```
   스프라이트+참고이미지 범용 기본값은 **`gemini-3.0-pro`(Nanobanana Pro)** — Nano Banana 계열 최고 품질. ※ 검증결과 3.0-pro 는 **불투명 플랫 배경**을 내므로(flash 와 달리 투명 네이티브 아님) 캐릭터/스프라이트로 쓸 땐 `--remove-bg` 필요; 풀씬(키비주얼/배경)은 불투명이 정상이라 `gen` 기본 removeBg=false. 투명 네이티브가 필요하거나 더 빠르게는 `gemini-3.1-flash`(Nano Banana 2). `gpt-image-1-5` 등은 화풍이 매끈하게 드리프트할 수 있어 픽셀아트엔 비권장. 화풍 특화가 필요하면 목록에서 고른다.
   모델은 화풍이 고정된 LoRA/서드파티라 **기존 아트와 100% 일치는 어렵다** — 참고이미지 + 모델 선택으로 근사한다.

3. **생성**
   ```bash
   .claude/skills/unity-ai-image-gen/scripts/uai.sh gen \
     --prompt "cute glossy bubble character, clean cartoon, transparent background" \
     --out Assets/Generated/my_bubble.png \
     --model gemini-3.0-pro \          # 기본값(Nanobanana Pro) — 생략 가능. 불투명 배경 제거는 --remove-bg
     --ref Assets/.../existing_reference.png      # 참고이미지(선택)
   ```
   비동기·수십초. 스크립트가 `GEN:DONE`/`GEN:ERROR` 까지 폴링하고 결과(경로·cost)를 반환한다.
   프롬프트·경로는 내부에서 base64 로 안전 전달되므로 따옴표/공백/한글을 그대로 써도 된다.

4. **(선택) 크기 맞추기** — 생성 해상도는 모델 고정(보통 1024²)이라 임의 크기로 *생성*은 안 된다.
   기존 에셋 크기에 맞추려면 후처리 리사이즈:
   ```bash
   .claude/skills/unity-ai-image-gen/scripts/uai.sh resize \
     --src Assets/Generated/my_bubble.png --dst Assets/Generated/my_bubble_108.png --w 108 --h 108
   ```

5. **결과 보고** — 저장된 에셋 경로와 소모 cost 를 사용자에게 알린다. 필요하면 Read 로 이미지를 띄워 보여준다.

## 사운드 · 애니메이션 · 스프라이트시트 (이미지 외 모달리티)

`gen`(이미지) 외에 동일 하네스(AiGenProbe + GenerationParameters 패턴)로 다음도 생성한다. 전부 포인트 소모이며 `GEN:DONE/ERROR` 폴링 동일.

- **효과음/오디오** → AudioClip:
  ```bash
  uai.sh sound --prompt "short punchy sci-fi laser blaster" --out Assets/Game/Audio/shot.wav --duration 1 [--loop]
  ```
  기본 `elevenlabs-sound-effects-v2`(효과음, 루프 지원). 음악은 `--model google-lyria-3-clip`(루프 30s)/`meta-musicgen`, 음성(TTS)은 `elevenlabs-multilingual-v2`.

- **모션/애니메이션** → AnimationClip (3D 휴머노이드 모션):
  ```bash
  uai.sh anim --prompt "idle breathing loop" --out Assets/Game/Anim/idle.anim --duration 3 [--video Assets/.../ref.mp4]
  ```
  기본 `unity-text-to-motion`/`uthana-video-to-motion`(영상→모션, `--video` 필요). **휴머노이드 리그 대상**이라 2D 게임엔 보통 부적합.

- **2D 프레임 애니메이션(캐릭터 모션)** → sprite-swap `AnimationClip`. **권장 = 영상 모델 시트 생성 한 방**(헤드리스 동작 검증됨):
  ```bash
  # 참조 캐릭터 → seedance 가 애니 영상→4×4(16) 프레임 시트 → (영상은 불투명 배경) 자동 키잉+슬라이스+PPU정규화+클립까지
  uai.sh spritesheet \
    --prompt "the same character hovering idle, gentle bob, no camera movement, seamless loop" \
    --ref   Assets/Game/Art/.../player.png \
    --sheet Assets/Game/Art/.../idle_sheet.png \
    --clip  Assets/Game/Anim/PlayerIdle.anim \
    --model video-seedance-1-pro --cols 4 --rows 4 --fps 12
  ```
  영상 모델(`video-seedance-1-pro`/`video-kling-v3-i2v-pro`)은 **원본 화풍을 잘 보존**하며 16프레임 시트를 만든다. `--clip` 을 주면 자동으로 **키잉(불투명 배경 투명화, `--no-key` 로 끔) → `ISpriteEditorDataProvider` 그리드 슬라이스 → PPU 100 정규화 → m_Sprite 스왑 클립**.

  **개별 포즈 세트**(idle/thrust/bank 등)는 Nano Banana 정적 gen 으로 1장씩(첫 장을 ref 로 체이닝해 일관성↑) → 한 시트로 합쳐 `clip`:
  ```bash
  uai.sh gen --prompt "...idle..."   --ref char.png       --out poses/idle.png      # gemini-3.0-pro(기본)
  uai.sh gen --prompt "...thrust..." --ref poses/idle.png --out poses/thrust.png    # idle 을 ref 로 체이닝
  # 4장을 2×2 시트로 합친(blit) 뒤:  uai.sh clip --sheet poses_sheet.png --out X.anim --cols 2 --rows 2 --fps 6
  ```

- **이미 만든 평면 시트 → 클립**(AI/포인트 0):
  ```bash
  uai.sh clip --sheet sheet.png --out X.anim --cols 4 --rows 4 --fps 12 [--ppu 100] [--key] [--no-loop]
  ```
  SpriteRenderer+Animator 에 클립을 물리면 재생(프로시저럴 transform/색 juice 와 합성 가능 — Animator 는 m_Sprite 만 건드림).

> ⚠️ 함정/노하우(검증됨):
> - **AI 생성 시트는 `spritePixelsPerUnit`=텍스처폭(1024)으로 임포트** → 슬라이스 시 화면에서 ~10배 작게 렌더. `--ppu 100`(기본)로 정규화. 검증: `sprite.bounds.y × visualScale ≈ 기존 캐릭터 worldH`.
> - **영상 모델 시트는 불투명 배경** → `spritesheet --clip` 은 키잉 기본 ON, 손수 만든 투명 시트엔 `clip`(키잉 off). 단색·평평할수록 깔끔(발광/소프트 엣지는 약간 헤일로 가능).
> - **슬라이싱은 `ISpriteEditorDataProvider`로** — Unity 6 에서 obsolete `TextureImporter.spritesheet` 는 자동슬라이스로 덮여 불안정(섬 N개). 하네스가 처리함.
> - **identity/화풍**: seedance=원본 보존 우수(프레임별 약간의 위치/스케일 변동 가능). 정적 gen 은 `gemini-3.0-pro`(Nanobanana Pro, 기본) 권장 — gpt-image-1-5 는 카툰 드리프트. 픽셀아트 보존이 특히 중요하면 `gemini-3.1-flash`.
> - **휴머노이드 3D 모션**이 필요하면 `anim`(2D 스프라이트엔 부적합). 파티클은 생성 모달리티 없음.
> - 모델/모달리티는 `uai.sh models | grep -iE 'sound|animat|video|sprite'` 로 확인.

## 출력

- 생성된 PNG 에셋이 `--out` 경로에 저장된다(스프라이트 텍스처로 임포트). 리사이즈본은 `--dst` 에 저장.
- 사용자에게는 **에셋 경로 + 소모 포인트 + (요청 시) 이미지 미리보기**를 보고한다.

## 주의 (중요)

- 이 경로는 **Unity 비공개 internal API + pre-release** 다. 패키지 업데이트로 타입/필드명이 바뀌면 하네스가 깨질 수 있다.
  `gen`/`models` 가 `NO_PROBE` 나 reflection 예외를 내면 먼저 `ensure` 재실행 → 그래도 실패면 `references/api-and-caveats.md` 의 API 표면과 실제 어셈블리를 대조해 패키지의 `Editor/AiGenProbe/AiGenProbe.cs` 를 갱신한다.
- cost 는 `GEN:STARTED cost=` 로 보고된다(비용 확인은 위 "비용 확인 정책" 참고).
- `scripts/uai.sh` 는 자체 CLI 전송(`unity command eval`/`recompile`)을 내장하며, 공식 Unity CLI(`~/.unity/bin/unity`)와 프로젝트 루트를 가정한다.

## 참고

- `references/api-and-caveats.md` — internal API 표면, 모델 목록 성격, CLI 전송(`unity command`) 비자명 동작.
- `claudedocs/unity-ai-image-generation.md` — 발견 경위·검증 결과 상세.
