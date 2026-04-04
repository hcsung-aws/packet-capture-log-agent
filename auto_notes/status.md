# Current Status & Gap Analysis

## Phase별 달성도 (PURPOSE-SCENARIO 대비)

| Phase | 목표 | 상태 | 비고 |
|-------|------|------|------|
| 1: 프로토콜 생성 | 게임 소스 → JSON 프로토콜 | ✅ | AgentCore (LLM 5-Phase), AWS 배포, CLI+웹 |
| 2: 캡처+파싱+로그 | TCP 캡처 → 동적 파싱 → 로그 | ✅ | 정수/문자열/배열/구조체/string_prefixed/conditional + Transform |
| 3: 시나리오 자동 조립 | 캡처 → 의미 분석 → 시나리오 | ✅ | BT 자동 생성+실행+편집, FSM, ScenarioBuilder, 범용화, explore phase |
| 4-1: 동기 부하 테스트 | --clients N 동시 재현 | ✅ | LoadTestRunner, E2E 검증 완료 |
| 4-2: async 전환 | 수천 동시 클라이언트 | ❌ | 미구현 |
| 4-3: 멀티 에이전트 | 수만 동시 클라이언트 | ❌ | 미구현 |

## Acceptance Criteria: 2/2 충족

## 코드 규모

| 영역 | 파일 | 라인 | 테스트 |
|------|------|------|--------|
| C# 소스 | 32 | 5,613 | 119개 (11파일, 2,445줄) |
| AgentCore Python | ~15 | 1,477 | 수동 E2E |
| AgentCore Terraform | 2 | 374 | — |

## 컴포넌트별 테스트 커버리지

| 컴포넌트 | 단위 테스트 | E2E |
|----------|-----------|-----|
| PacketParser/Builder/Formatter | ✅ | ✅ |
| PacketReplayer (ParseLog) | ✅ | ✅ |
| SequenceAnalyzer | ✅ | ✅ |
| ActionCatalogBuilder | ✅ | ✅ |
| ScenarioBuilder | ✅ | ✅ |
| BehaviorTreeBuilder/Executor | ❌ | ✅ (수동) |
| FsmBuilder/Executor | ❌ | ✅ (수동) |
| LoadTestRunner | ❌ | ✅ (수동) |
| AgentCore | ❌ | ✅ (수동) |

## Last Updated
2026-04-04, Mickey 18
