# MICKEY-12-HANDOFF

## Current Status
Action Catalog 완성: 배열 flat key 파싱 → Dynamic Field 자동 감지 (suffix 타입 필터 + 시간 순서 + 수동 오버라이드) → 의미 단위 분할 + merge 저장. Q 키 캡처 종료 시 자동 분석. 테스트 107개 통과.

## Next Steps
Phase 3 마무리: Action Catalog의 Action들을 조립하여 재현 가능한 시나리오 파일 생성.

## Important Context
- actions/{protocol}_actions.json에 카탈로그 저장, --analyze 시 merge 전략으로 누적
- field_mappings (프로토콜 JSON): 수동 매핑 오버라이드 (external/static 지원)
- suffix 타입 필터: *Uid↔*Uid, *Id↔*Id, slot↔slot만 매칭
- 시간 순서 필수: SEND 이전 RECV만 후보 (결과 패킷이 소스로 잡히는 버그 수정 완료)

## Quick Reference
- SESSION: MICKEY-12-SESSION.md
- 캡처+분석: Q 키로 종료 시 자동 실행
- 수동 분석: `PacketCaptureAgent.exe -p protocol.json --analyze capture.log`
- 테스트: `"/mnt/c/Program Files/dotnet/dotnet.exe" test PacketCaptureAgent.Tests --no-restore -v quiet`
- Context window: 40%
