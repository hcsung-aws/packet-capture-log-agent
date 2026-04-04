# MICKEY-13-HANDOFF

## Current Status
Phase 3 완료: ScenarioBuilder + DynamicFieldInterceptor 구현, E2E 검증 통과. 캡처→카탈로그→시나리오 생성→재현 전체 파이프라인 동작. 테스트 119개.

## Next Steps
Phase 4: 다중 클라이언트 동시 재현 (부하/회귀 테스트).

## Important Context
- 시나리오 기능은 기존 코드와 완전 분리 (ScenarioBuilder.cs만, 기존 파일 수정 없음)
- --build-scenario: 인터랙티브 시나리오 생성, -s: 시나리오 재현
- Attendance 카테고리: phases에서 제거 → 주변 Phase 상속 (컨텍스트 의존적 패킷 처리 패턴)
- captures/archive/에 최근 5개 캡처 백업, 나머지 삭제 완료

## Quick Reference
- SESSION: MICKEY-13-SESSION.md
- 시나리오 생성: `PacketCaptureAgent.exe -p protocol.json --build-scenario`
- 시나리오 재현: `PacketCaptureAgent.exe -p protocol.json -s scenario.json -t host:port`
- 테스트: `"/mnt/c/Program Files/dotnet/dotnet.exe" test PacketCaptureAgent.Tests --no-restore -v quiet`
- Context window: 25%
