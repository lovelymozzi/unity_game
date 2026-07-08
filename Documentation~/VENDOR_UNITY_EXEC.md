# 설계 — unity-exec 번들(vendoring) into HwiFoundation (2026-07-08)

## 목표
파운데이션 패키지 **하나만 설치하면 Unity Editor C# exec 서버 + AI 스킬까지** 쓸 수 있게 한다.
신규 프로젝트는 표준 `com.linestudio.unity-exec` git 의존 없이 이 번들 exec 를 사용한다.

## 확정 결정
- **범위:** Editor C# 서버(9 .cs) + AI 스킬(`ClaudeSkill~`) + `bootstrap.sh`/`unity-exec.sh` 전체.
- **공존:** 불필요 — 신규 프로젝트는 번들 exec 만 사용(표준 패키지 미설치).
- **방식:** **verbatim vendoring**(원본 그대로). ns `UnityExec`, asmdef `UnityExec.Editor`, 런타임 `~/.unity-exec/`·포트 8090, 메뉴 `Tools > UnityExec`, 자동 기동 — 전부 원본 유지. → 코드 수정 0, 스킬 스크립트 무수정 동작, 업스트림 재동기화 간단.
- **Newtonsoft:** `com.unity.nuget.newtonsoft-json 3.2.1` 를 파운데이션 package.json 의존에 추가.
- **출처:** LINE Studio `unity-exec-cli` @ `51c764b` (내부 패키지, LICENSE 파일 없음 → 조직 내 vendoring, 출처 표기).

## 레이아웃
```
Editor/Exec/                    ← Unity 임포트(Editor 전용)
  UnityExec.Editor.asmdef       (원본, precompiledRef Newtonsoft.Json.dll)
  *.cs ×9                       (AuditLogger/AuthManager/CsharpExecutor/ExecHttpServer/
                                 ExecMenu/ExecSettings/LogCapture/ResponseTypes/SecurityPolicy)
Exec~/                          ← Unity 미임포트(~), copy-on-install 도구
  bootstrap.sh, unity-exec.sh
  ClaudeSkill~/ (SKILL.md, scripts/, references/)
```

## 소비자 계약
- 신규 프로젝트: `com.hwi.foundation` 설치 → Editor 열면 exec 서버 자동 기동(포트 8090~).
- AI 스킬 설치: `bash <pkg>/Exec~/bootstrap.sh "$PWD" --skip-manifest`
  (`--skip-manifest`: exec 는 파운데이션에 번들이므로 manifest 에 git 의존 추가 불필요.)
- `Templates~/manifest.snippet.json` 에서 `com.linestudio.unity-exec` 제거.
- ⚠ 표준 `com.linestudio.unity-exec` 를 **동시에 설치 금지**(asmdef `UnityExec.Editor` 중복 → 컴파일 붕괴).

## 재동기화(업스트림 반영)
```
git clone --depth 1 https://git.linecorp.com/LINEStudio-Client/unity-exec-cli.git /tmp/uexec
cp /tmp/uexec/Editor/*.cs /tmp/uexec/Editor/*.asmdef  Editor/Exec/
cp -R /tmp/uexec/ClaudeSkill~ /tmp/uexec/bootstrap.sh /tmp/uexec/unity-exec.sh  Exec~/
```
verbatim 이라 병합 충돌 없음. 새 상단 커밋 해시를 이 문서·README 에 갱신.

## 검증 (TestProject~)
1. TestProject~ manifest 에서 `com.linestudio.unity-exec` 제거(중복 회피).
2. 파운데이션에 Newtonsoft 의존 추가 → 재컴파일 green.
3. 번들 exec 서버 자동 기동 + `/status` 응답 확인.
