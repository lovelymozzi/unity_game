# AI 툴링 — 공식 Unity CLI 기반 (HwiFoundation)

파운데이션은 AI 에이전트(Claude Code 등)가 Unity 에디터를 조작·검증·생성하도록 스킬을 번들한다.
전송수단은 **공식 Unity CLI**(`unity` 바이너리 + `com.unity.pipeline` 패키지)다 — 사내 HTTP 서버·토큰·포트 없음.

## 구성

```
Skills~/                         ← Unity 미임포트(~), copy-on-install
  bootstrap.sh                   스킬 설치기(형제 스킬 dir 를 .claude/skills/ 로 복사 + CLAUDE.md 가이드 블록)
  unity-cli/                     에디터 구동·검증·조작(전송수단 정본, 스크립트 0)
  playtest/                      좌표 기반 Play Mode 테스트 + 리포트 (+ references/ 게임별 레시피)
  unity-ai-image-gen/            Unity AI Generators 로 이미지/사운드/애니 생성
Editor/AiGenProbe/               ← Unity 임포트(Editor 전용). image-gen 하네스(리플렉션 대상).
  AiGenProbe.cs, AiGenProbe.asmdef ("AiGenProbe.Editor", refs Unity.2D.Sprite.Editor, autoReferenced:false)
Tools~/foundation-setup.sh       오케스트레이터: 컨벤션 주입 + Skills~/bootstrap.sh 위임
```

`unity-cli` 는 프로젝트에 스크립트를 남기지 않는다(CLI 가 곧 전송수단). `playtest`/`unity-ai-image-gen` 만
`scripts/` 를 `.claude/skills/` 로 설치하며, 이미지 생성 하네스 `AiGenProbe` 는 패키지 Editor 어셈블리로 임포트된다.

## 설치 (소비 프로젝트)

```bash
# 한 명령(권장) — 컨벤션 + 스킬 + 가이드 블록
bash Packages/com.hwi.foundation/Tools~/foundation-setup.sh "$PWD"

# 스킬만
bash Packages/com.hwi.foundation/Skills~/bootstrap.sh "$PWD"
```

`.claude/skills/{unity-cli,playtest,unity-ai-image-gen}/` 설치 + `CLAUDE.md` 의 `<!-- hwi-unity-cli-skill:* -->`
마커 블록 주입(멱등). 컨벤션 블록(`<!-- hwi-foundation:* -->`)과 독립.

## 머신 1회 셋업 (프로젝트마다 반복 X)

```bash
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
unity auth login            # 브라우저 로그인 (사람)
unity license              # 없으면 unity license activate
unity pipeline install      # 프로젝트 열고 com.unity.pipeline(experimental) 설치
unity status --format json  # 확인
```

## 히스토리 (unity-exec → 공식 Unity CLI)

- 이전 버전은 사내 `com.linestudio.unity-exec`(Editor `[InitializeOnLoad]` HTTP 서버, 포트 8090~,
  `~/.unity-exec/` 토큰·인스턴스 매핑)를 verbatim vendoring 해 `Editor/Exec/`(9 .cs, asmdef `UnityExec.Editor`)
  + `Exec~/`(bootstrap.sh/unity-exec.sh/ClaudeSkill~) 로 번들했다.
- 범용 파운데이션에는 사내 전용 서버 패키지가 부적합하고, 스킬 스크립트가 포트/토큰/전송 스크립트에 결합돼 이식성이 떨어졌다.
- → **공식 Unity CLI 로 전환**: `Editor/Exec/` + `Exec~/` 제거, 스킬 전송을 `unity command`(eval/recompile/…)로 교체.
  `unity-editor-ops` 스킬은 스크립트 없는 `unity-cli` 스킬로 대체. `playtest`/`unity-ai-image-gen` 은 전송만 CLI 로 바꾸고 명령 표면은 유지.
- Newtonsoft(`com.unity.nuget.newtonsoft-json`)는 exec 서버 전용 의존이었고 제거됐다 — 패키지 코드 사용처 0(Save 는 `UnityEngine.JsonUtility`), Addressables 2.9.1·2d.sprite·inputsystem 모두 비의존(실측). 소비 게임 코드가 Newtonsoft 를 쓰면 소비 프로젝트가 직접 선언한다.

## 주의

- `com.unity.pipeline` · `unity` CLI 는 Unity **베타/experimental** — 명령 표면이 바뀔 수 있다. 불명확하면
  `unity command`(목록)·`unity <cmd> --help` 로 실측 확인. 버전 정본은 `unity --version`.
- 스킬은 게임 무관하게 유지한다. 게임 종속 로직(월드 입력 어댑터·인게임 오토플레이)은 `playtest/references/` 의
  레시피 + 템플릿으로만 제공하고, 코어 스크립트에 특정 게임 타입/이름을 넣지 않는다.
