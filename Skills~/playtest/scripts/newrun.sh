#!/usr/bin/env bash
# playtest: 리포트 run 폴더 생성 + report.md/progress.md 스켈레톤. RUNDIR 경로를 stdout으로 반환.
# 사용: newrun.sh <slug> ["테스트 의도 한 줄"]
set -euo pipefail
SLUG="${1:?usage: newrun.sh <slug> [intent]}"
INTENT="${2:-}"
# 프로젝트 루트(= 이 스크립트 기준 ../../../..) 하위 claudedocs 에 저장
ROOT="$(cd "$(dirname "$0")/../../../.." && pwd)"
TS="$(date +%Y-%m-%d_%H%M)"
RUNDIR="$ROOT/claudedocs/playtest-reports/${TS}-${SLUG}"
mkdir -p "$RUNDIR/shots"

cat > "$RUNDIR/report.md" <<EOF
# Playtest 리포트 — ${SLUG}

- 실행: ${TS}
- 판정: ⏳ (진행 중)

## 요약
<!-- 성공/실패 한 줄 + 근거 -->

## 테스트 시나리오
- 의도: ${INTENT}
- 성공 기준:

## 스텝 타임라인
<!-- progress.md 종합 -->

## 데이터 흐름 추적
<!-- 상태 전이 / Animator·파티클(모션) / 게임 고유 데이터 변화 등 비시각 항목 -->

## 실패 시 원인 분석
<!-- 어디서·왜 (콘솔 에러 + 프로브 근거) -->

## 재현 / 주의 메모
EOF

cat > "$RUNDIR/progress.md" <<EOF
# 진행 로그 — ${SLUG} (${TS})

의도: ${INTENT}

---
EOF

echo "$RUNDIR"
