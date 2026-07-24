---
name: unity-cli
description: Use this skill whenever a request involves Unity Editor inspection, verification, diagnosis, or editor-side manipulation via the official Unity CLI. Trigger on natural requests such as checking prefab field wiring/null references, validating component or GameObject existence, inspecting scene/asset/project state, analyzing compile errors or console logs, running C# in editor context (`eval`), or safely modifying editor objects/assets/scenes and re-validating results.
---

# Unity CLI (에디터 구동 · 검증 · 조작)

실행 중인 Unity 에디터를 **공식 Unity CLI**(`unity` 바이너리 + `com.unity.pipeline` 패키지)로 구동한다.
`playtest`·`unity-ai-image-gen` 스킬은 이 CLI 위에서 각자 동작하며, 이 문서는 **공통 사용법·검증 게이트·안전 수칙**을 정의한다. (별도 스크립트를 두지 않는다 — CLI가 곧 전송수단이다.)

## 전제 (콜드 부트스트랩 — 위에서부터 감지→충족)

아래 순서로 **감지 커맨드 → 미충족 시 대응**. **[사람]** 표시는 대화형이라 Claude가 대신 못 함(사용자 1회 실행). 나머지는 Claude가 자동 수행 가능.

1. **`jq`** (예시·JSON 파싱 필수): `command -v jq` → 없으면 `brew install jq`(macOS) / 배포판 패키지매니저.
2. **공식 CLI**: `unity --version` (`~/.unity/bin/unity`, PATH) → 없으면
   `curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash` (설치 후 새 셸 또는 `source ~/.zshrc`).
3. **Unity 계정 로그인** [사람]: `unity auth status` → 미로그인 시 `unity auth login`(브라우저 플로우). 키체인 공유라 1회만.
4. **라이선스** [사람일 수 있음]: `unity license` → 없으면 `unity license activate`(Personal/serial/floating).
5. **에디터 버전**: 대상 = 프로젝트 `ProjectSettings/ProjectVersion.txt` 의 `m_EditorVersion`. `unity editors` 목록에 있는지 → 없으면 `unity install <version>`.
6. **유효한 Unity 프로젝트**: `ProjectSettings/ProjectVersion.txt` 존재해야 함. 진짜 빈 폴더면 `unity projects create <name>` 로 생성(또는 기존 프로젝트로 이동).
7. **Pipeline 패키지**: 프로젝트 `Packages/manifest.json` 에 `com.unity.pipeline`(experimental). 없으면 `unity pipeline install`.
8. **에디터 실행 + Pipeline 서버**: `unity status`(port/state/playMode) 또는 `unity pipeline list`(serverReachable) → 미실행 시 콜드스타트 `unity open <projectPath>`(버전 자동 해석, 크로스플랫폼). 임포트/도메인로드로 수 분 — 무거우니 사용자 확인 후. Pipeline 첫 설치 직후엔 패키지 임포트까지 대기(서버가 늦게 뜸).

8개 충족되면 아래 "핵심 사용법" 으로 바로 사용. (일반 개발 머신은 1·3·4·5가 이미 갖춰진 경우가 많아 2→7→8만 자동 수행하면 됨.)

## 핵심 사용법

```bash
# 연결된 에디터 상태 (port/state/version/PID)
unity status --format json

# 사용 가능한 명령 목록(Pipeline 카탈로그) — 인자 없이
unity command --project-path . --format json

# 임의 C# 실행 (문장 + 마지막 return expr;)  ※ 계약은 아래 참고
unity command eval --project-path . --format json 'return UnityEngine.Application.unityVersion;'

# 큰/멀티라인 스니펫은 파일로
unity command eval_file --project-path . <path.cs>
```

- 결과는 `--format json` 의 `.data.result` 에 담긴다. `eval` 의 **C# 반환값**은 `.data.result.result`, 성공여부는 `.data.result.success`, 컴파일 진단은 `.data.result.diagnostics[]`.
- `--project-path .` 로 대상 인스턴스를 고정(생략 시 cwd 자동감지). `--timeout <sec>` 기본 30. `--runtime <name>` 으로 실행 중인 Player 에도 붙을 수 있음.

### eval 계약 (필수)
- 코드는 **메서드 바디**로 감싸진다 → **문장(statement)** 이어야 하고, 값이 필요하면 **마지막에 `return <expr>;`**. `bare 식`(예: `Application.unityVersion`)은 `CS1002` 로 실패한다.
- **타입은 완전 한정**(`UnityEditor.AssetDatabase`, `UnityEngine.ScreenCapture`). `using` 디렉티브는 바디에 못 넣는다.
- 리플렉션으로 internal/hidden 타입 접근 가능(`AppDomain.CurrentDomain.GetAssemblies()...`).
- **샌드박스 없음**: `eval` 은 프로젝트가 부를 수 있는 모든 API 도달(파일 IO·Process 포함). 파괴적 작업은 아래 안전 수칙을 따른다.

## 검증 게이트 (완료 판정 전 필수)

**`.cs` 수정 후 컴파일 검증** — 전용 first-class 명령 사용:
```bash
unity command recompile --project-path .                 # 트리거(변경분)
# 도메인 리로드 관통 폴링 — completed|up_to_date 될 때까지
unity command recompile_status --project-path . --format json
```
- `recompile_status` 결과: `{status: idle|triggered|compiling|completed|up_to_date, failed: bool, errors:[...]}`. `failed:true` + `errors[]` 로 판정(구조화됨 — 콘솔 파싱 불필요).
- **도메인 리로드 내성**: 리로드 창(~3–5초) 동안 `command` 호출이 일시 실패할 수 있다 → **에러를 폴링으로 흡수**하고 재시도(자동 대기 아님). `editor_status.domainReloadInProgress` 로 확인.
- **컴파일 통과만으로 완료 아님** — 영향 로직·직렬화까지 확인한다.

**prefab/scene/직렬화 검증** — first-class 명령(리플렉션 스니펫 대신 우선):
```bash
unity command get_serialized_fields --project-path . --target <handle|path> [--field <prop>]
unity command find_gameobjects  --project-path . [--name N] [--type T] [--hierarchy_path P]
unity command find_assets       --project-path . [--type T] [--name N]
unity command get_console_logs  --project-path . --severity error --limit 20
```
- 결정적 결과 보고: `OK | NULL | FIELD_MISSING | TYPE_MISMATCH` 스타일.

## 화면/플레이 (playtest 가 재사용)

```bash
unity command screenshot   --project-path . --view game --output /tmp/uitest/shot.png   # /tmp 등 프로젝트 밖 OK
unity command capture_game_view --project-path . --save_path <프로젝트 내부 경로>        # base64 반환, project-root 제한
unity command editor_play  --project-path .   # Play 진입
unity command editor_stop  --project-path .   # Play 종료
unity command editor_status --project-path . --format json   # status/compiling/domainReloadInProgress/playMode
```

## 안전 수칙 (샌드박스 부재 보완)

- **파괴적/광범위 작업 전 미리보기·확인**: 대량 에셋 변경, `delete_asset`/`delete_gameobject`, `switch_build_target`, `set_player_settings` 등은 `--dry_run`(지원 시) 로 먼저 확인하고 `--confirm true` 는 사용자 승인 후.
- 가장 작은 되돌릴 수 있는 변경부터. 변경 후 검증 재실행.
- 많은 에셋에 영향 가능하면 프리뷰 수집 후 **사용자 확인**.

## first-class 명령 카탈로그

`unity command --project-path . --format json` 이 정본 목록(설치 버전마다 다름). 도메인별 요약은 `references/cli-usage.md` 참고 — inspect(get_*/find_*/list_*), manipulate(create_*/set_*/delete_*/*_prefab), scene/asset, animator/timeline, lighting/navmesh/occlusion, package/build/test, editor 제어(editor_play/pause/stop/status), 그리고 `eval`/`eval_file`.

## 참고

- `references/cli-usage.md` — 명령 카탈로그(도메인별), eval 레시피, 도메인리로드/async 폴링 패턴, 오류 진단.
- 버전 정본은 `unity --version` / `unity changelog`. 베타(experimental)라 명령 표면이 바뀔 수 있으니 불명확하면 `unity command`(목록)·`unity <cmd> --help` 로 실측 확인.
