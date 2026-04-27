# MICKEY-31-HANDOFF

## Current Status
커버리지 리포팅 전체 완성 + E2E 검증 완료. 257개 테스트 통과. FSM/BT 멀티 클라이언트 + 실제 수신 추적까지 구현.

## Next Steps
1. 리포트 병합 도구 (여러 JSON 리포트 → 통합 커버리지 산출)
2. --timeout 옵션 BT/FSM 모드 지원 (현재 하드코딩 5000ms → CLI 제어)
3. CS_HEARTBEAT 자동 전송 로직 (heartbeat 커버리지 확보)

## Important Context
- 커버리지 추적 이중화: 액션 정의 RECV(ActionExecutor) + 실제 수신(ParsingResponseHandler). 둘 다 같은 tracker에 기록.
- 멀티 클라이언트: FsmMode/BtMode에서 --clients N 지원. 각 클라이언트 독립 TCP, tracker만 공유(thread-safe lock).
- E2E 결과: BT Nodes 100%, FSM States 100%(5 clients), Packets ~84%. Missing은 대부분 멀티 플레이어/시스템 패킷.
- CS_CHAR_LIST는 프로토콜에 정의되어 있지만 실제 게임 흐름에서 미사용 (서버가 로그인 시 자동 전송).
- Release 리빌드 주의: dotnet test는 Debug만 빌드. 실행 테스트 전 `dotnet build -c Release` 필수.

## Quick Reference
- SESSION: MICKEY-31-SESSION.md
- Tests: 257개 (기존 236 + 신규 21)
- Context window: 높음
