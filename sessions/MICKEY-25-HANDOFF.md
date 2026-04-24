# MICKEY-25-HANDOFF

## Current Status
프록시 모드 E2E 검증 완료 (FSM takeover 정상, BT 차단). 버그픽스 3건 + 코드 정리. 테스트 212개 통과.

## Next Steps
CI/CD 파이프라인 (GitHub Actions). 프록시 HTTP API (외부 스크립트 제어).

## Important Context
- 프록시 takeover 핵심 이슈: 같은 NetworkStream을 FSM과 RelayAsync가 동시에 읽으면 경합 → ForwardingResponseHandler + relay 일시중단으로 해결
- BT는 프록시 takeover에 부적합 (순차 실행 전제, 중간 진입 시 완료 액션 재실행) → ProxyMode에서 차단
- GameClient 포트 파라미터화 완료 (mmorpg_simulator 쪽)

## Quick Reference
- SESSION: MICKEY-25-SESSION.md + MICKEY-25-SESSION-continued.md
- Context window: 높음
