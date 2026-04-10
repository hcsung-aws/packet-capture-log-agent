# MICKEY-25-HANDOFF

## Current Status
async 전환 완료 + 프록시 모드 구현 (패스스루 + takeover + FSM/BT 상태 동기화). 테스트 212개 통과.

## Next Steps
프록시 모드 E2E 테스트 (mmorpg-simulator 연동). PURPOSE-SCENARIO에 프록시 시나리오 추가. ShowUsage에 프록시 도움말 추가.

## Important Context
- 프록시 모드 핵심 설계: PacketObserver가 패스스루 중 SEND 패킷→액션 역매핑으로 FSM/BT 상태를 동기화. takeover 시 기존 TCP 연결 + GameWorldState/SessionState를 그대로 FSM/BT Executor에 전달하여 현재 상태에서 실행 재개.
- FsmExecutor/BtExecutor에 ExecuteOnStreamAsync 오버로드 추가 (기존 연결 + 시작 상태/preObserved)

## Quick Reference
- SESSION: MICKEY-25-SESSION.md
- Context window: 높음
