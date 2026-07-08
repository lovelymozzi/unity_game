---
name: unity-editor-ops
description: Use this skill whenever a request involves Unity Editor inspection, verification, diagnosis, or editor-side manipulation via unity-exec. Trigger on natural requests such as checking prefab field wiring/null references, validating component or GameObject existence, inspecting scene/asset/project state, analyzing compile errors or console logs, running C# snippets in editor context, or safely modifying editor objects/assets/scenes and re-validating results.
---

# Unity Editor Ops

Use this skill as the default runbook whenever Unity Editor state or data must be inspected or manipulated from terminal/agent workflows.

> **Installation note**: This skill is shipped with the `com.linestudio.unity-exec` Unity package and installed into the project at `.claude/skills/unity-editor-ops/` by the package's `bootstrap.sh`. All paths below assume that location.

## 에디터 띄우기 (cold start)

unity-exec 는 **열려 있는 Editor 안에서** 동작한다. Editor 가 안 떠 있어 인스턴스가 없으면(`resolve-port.sh` 가 매치 실패) 먼저 띄운다:

```bash
# 프로젝트 루트(또는 하위)에서 — 버전 자동 감지 후 ready 까지 대기
.claude/skills/unity-editor-ops/scripts/launch.sh
```

- `ProjectSettings/ProjectVersion.txt` 의 `m_EditorVersion` 으로 Unity Hub 설치본을 찾아 실행한다 (버전 하드코딩 금지).
- 이미 해당 프로젝트 인스턴스가 살아있으면 **재실행하지 않고** 즉시 ready 보고 후 종료한다.
- Hub 설치 루트가 기본값(`/Applications/Unity/Hub/Editor`)과 다르면 `UNITY_EDITOR_ROOT` 로 지정.
- macOS 전용(`open -a`). Exit: 0=ready, 1=경로/버전/에디터 없음, 2=timeout.
- 콜드 스타트는 임포트/도메인 로드로 수 분 걸릴 수 있다. 무거운 작업이라 사용자 요청·확인 후 실행한다.

## Core Workflow

1. Run preflight first.
- Resolve port from cwd: `PORT=$(.claude/skills/unity-editor-ops/scripts/resolve-port.sh)` (베이스 포트 8090은 충돌 시 8091/8092…로 자동 분배되므로 절대 하드코딩하지 말 것)
- 매치되는 인스턴스가 없으면(Editor 미실행) 위 "에디터 띄우기" 의 `launch.sh` 로 먼저 띄운다.
- Check server: `curl -s "http://127.0.0.1:$PORT/status"`
- Read token each time: `TOKEN=$(cat ~/.unity-exec/auth-token)`
- If `state=compiling`, wait and retry.

2. Execute with safe payload encoding.
- Prefer `.claude/skills/unity-editor-ops/scripts/uexec.sh` to avoid JSON quoting mistakes.
- Use explicit `usings` only when needed.

3. **.cs 파일 수정 후 컴파일 검증** (필수).
- `ucompile.sh` 스크립트를 사용한다 (`GET /compile` 1회 조회 금지).
- exit code 0이면 성공, 1이면 에러 목록 확인.
- 상세는 아래 "컴파일 검증" 섹션 참고.

4. Diagnose failures in fixed order.
- `/exec` failure → `ucompile.sh` → check `/logs`.
- Distinguish compile errors, security policy violations, timeout, queue/rate limits.
- See `.claude/skills/unity-editor-ops/references/error-diagnosis.md` for full diagnosis guide.

5. Report deterministic results.
- Return concrete values and absolute paths.
- For validation checks, use `OK | NULL | FIELD_MISSING | TYPE_MISMATCH` style statuses.

## Task Patterns

### 1) Inspect / Query

Use for read-only checks:
- Editor status, active scene, compile state
- Prefab/component existence
- Serialized field assignment/null checks
- Asset lookup by path/GUID

### 2) Validate / Diagnose

Use for regression checks:
- Expected object/component counts
- Required references not-null
- Compile errors/warnings summary
- Recent console errors by level

### 3) Editor Manipulation

Use for controlled Unity-side changes when requested:
- Open scene, create/move/select objects
- Add/remove components
- Adjust serialized values
- Save scene/asset changes via UnityEditor APIs

Rules for manipulation:
- Require explicit target path/object criteria before writing changes.
- Apply smallest reversible change first.
- Re-run validation after modification.
- If operation could affect many assets, gather preview info first and **confirm with user** before proceeding.

## 컴파일 검증

**.cs 파일을 수정한 뒤에는 반드시 컴파일 검증을 수행한다. 기본은 Refresh 기반(변경분만) `ucompile.sh` 다.**

```bash
# 표준 검증: AssetDatabase.Refresh로 변경분만 재컴파일 → 완료 대기 → 결과 반환
.claude/skills/unity-editor-ops/scripts/ucompile.sh

# 현재 진행 중인 컴파일만 대기 (트리거 없이)
.claude/skills/unity-editor-ops/scripts/ucompile.sh --no-trigger

# 타임아웃/폴링 간격 조정
.claude/skills/unity-editor-ops/scripts/ucompile.sh --timeout 180 --poll 3

# 폴백: Refresh가 변경을 전혀 감지 못하는 드문 경우에만 전체 리컴파일 강제 (느림)
.claude/skills/unity-editor-ops/scripts/ucompile.sh --full
```

- **`--full`은 기본값이 아니다.** 전체 리컴파일은 느리고(수십 초+) 대개 불필요 — 수정 파일이 명확하면 기본 Refresh 모드로 검증한다.
- 통과로 판단하기 전 **결과 freshness 확인**: 폴링 중 도메인 리로드(`server down→back online`)가 보였거나, 반환 `lastCompileTime`이 트리거 시각 이후면 실제 재컴파일이 일어난 것. 둘 다 아니고 변경이 안 잡힌 의심이 들 때만 `--full`로 폴백.
- **exit code**: 0=success, 1=failed(에러 있음), 2=timeout, 3=server error
- 결과 JSON에 `errorCount`, `errors[]` 배열이 포함됨
- domain reload 중 서버 다운을 자동 감지하고 재연결 대기함
- `GET /compile`만 1회 조회하는 것은 **금지** — domain reload로 결과를 놓칠 수 있음

## Unity-Exec API

```bash
# 포트는 항상 resolve-port.sh로 — 8090은 베이스일 뿐 인스턴스마다 다름
PORT=$(.claude/skills/unity-editor-ops/scripts/resolve-port.sh) || exit 1
TOKEN=$(cat ~/.unity-exec/auth-token)

# 서버 상태 확인 (인증 불필요)
curl -s "http://127.0.0.1:$PORT/status"

# C# 코드 실행
curl -X POST "http://127.0.0.1:$PORT/exec" \
  -H "X-Auth-Token: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"code":"Application.unityVersion"}'

# 컴파일 상태 확인 (GET: 현재 상태 조회)
curl -s "http://127.0.0.1:$PORT/compile" -H "X-Auth-Token: $TOKEN"

# 컴파일 트리거 (POST: AssetDatabase.Refresh 후 즉시 반환, 결과는 GET으로 폴링)
curl -X POST "http://127.0.0.1:$PORT/compile" \
  -H "X-Auth-Token: $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}'                    # 변경 감지 모드 (기본)
# -d '{"full":true}'        # 전체 리컴파일 강제

# 에디터 로그 확인
curl -s "http://127.0.0.1:$PORT/logs?count=50&level=error" -H "X-Auth-Token: $TOKEN"
```

헬퍼 스크립트:
- `.claude/skills/unity-editor-ops/scripts/launch.sh` — Editor 콜드 스타트 + unity-exec ready 대기 (버전 자동 감지)
- `.claude/skills/unity-editor-ops/scripts/uexec.sh` — C# 코드 실행 (JSON 이스케이핑 안전 처리)
- `.claude/skills/unity-editor-ops/scripts/ucompile.sh` — 컴파일 트리거 + 완료 대기 + 결과 반환
- `.claude/skills/unity-editor-ops/scripts/preflight.sh` — 서버/토큰 사전 점검

## Unity-Exec Constraints

허용: `UnityEngine.*`, `UnityEditor.*`, `System.Linq`, `System.Collections.*`, `System.Text.*`, `System` (정확히 이것만)
차단: `Process.Start`, `HttpClient`, `Assembly.Load`, `File.Delete`, `File.WriteAllText`, `TcpClient`

- 기본 코드 크기 제한: 10KB
- Rate limit / queue full → 429 반환 시 backoff 후 재시도
- 408 timeout 시 코드 범위 축소 후 재시도
- 보안 정책 위반 시 허용 API로 재작성

## Reusable Snippets

자주 쓰는 코드 패턴은 `.claude/skills/unity-editor-ops/references/unity-operations-recipes.md` 참고:
- 현재 씬/컴파일 상태 조회
- 프리팹 + 컴포넌트 존재 확인
- Private 직렬화 필드 null 체크 (Reflection)
- 씬 오브젝트 생성/선택/저장
- 배치 작업 전 에셋 프리뷰
