# MICKEY-17-HANDOFF

## Current Status
범용화 3단계 완료: BT Builder 범용화 + 타입 확장 + 프로토콜 자동 생성(AgentCore) 전체 구현. field_variants로 이동 방향 고정 문제 해결. 문서 전체 최신화.

## Next Steps
1. 모델 비교 (Haiku/Sonnet 3.7 vs Sonnet 4 — 비용 대비 효율)
2. field_variants 적용 후 BT 재생성 + 이동 다양화 검증 (--analyze 재실행 → --build-behavior)

## Important Context
- AgentCore 설계 논의 상세: 사용자가 "전혀 새로운 게임에도 수동 작업 거의 없이" 동작을 핵심 요구. Option A(규칙 기반)는 Kiro CLI와 다를 바 없으니 의미 없다고 판단 → Option B(LLM Agent) 선택. AgentCore는 AWS 클라우드, 로컬은 클라이언트만. 멀티 에이전트 상호 검증 + 단계별 독립 실행 + 파이프라인 래핑이 설계 원칙.
- field_variants 근본 원인: ActionCatalogBuilder에서 같은 actionId 반복 시 RepeatCount++만 하고 필드값 무시. CS_MOVE의 dirX/dirY가 첫 번째 값(-1, 0)으로 고정됨. dynamic_fields 감지도 안 됨 (이동 방향은 RECV에서 오지 않는 사용자 입력).
- 녹화/카탈로그/BT 경로: 프로토콜 파일의 부모 디렉토리(protocols/)의 한 단계 위(프로젝트 루트) 기준으로 recordings/, actions/, behaviors/ 참조.
- 세션 로그 상세도 피드백: 사용자가 "설계 논의 내용이 과도하게 요약되어 다음 세션에서 유실됨" 지적. 핸드오프/세션 로그에 설계 논의 정황을 더 포함해야 함.

## Protocol Feedback
- 세션 로그/핸드오프에 설계 논의 상세 내용 포함 필요 (REMEMBER 후보)

## Quick Reference
- SESSION: MICKEY-17-SESSION.md
- AgentCore 아키텍처: context_rule/agentcore-architecture.md
- 테스트: `dotnet test PacketCaptureAgent.Tests --no-restore -v quiet` (119개)
- AgentCore CLI: `cd agent-core/client && python3 cli.py generate --source /path`
- AgentCore 웹: `cd agent-core/client && python3 app.py 8090`
- 일괄 분석: `.\analyze_all.ps1 protocol.json captures_dir`
- Context window: ~45%
