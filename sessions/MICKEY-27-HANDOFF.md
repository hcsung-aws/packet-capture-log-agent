# MICKEY-27-HANDOFF

## Current Status
목업 서버(방안 B: 상태 추적) 구현 완료. 클라이언트 접속 E2E 검증 완료 (캐릭터 생성/선택/이동/NPC 표시 정상). 테스트 227개 통과.

## Next Steps
E2E 테스트(BT/FSM → 목업 서버), 필요 시 퀘스트/파티 상태 추적 확장, README 목업 서버 사용법 추가.

## Important Context
- 배열 직렬화: PacketBuilder.GetList()는 List<object>만 인식. List<Dictionary>는 빈 리스트 처리됨 → (object) 캐스트 필수
- field_ranges: recordings recv_state에서 S2C 필드 min/max 추출. 스폰 좌표/HP 생성에 사용. 이동 경계 제한에는 부적합 (관측 범위 ≠ 맵 경계) — 사용자와 합의하여 현 상태 유지
- 1차 상태 추적 범위: 이동/전투/상점/인벤토리 = 동적, 퀘스트/파티/채팅 = 고정 응답

## Quick Reference
- SESSION: MICKEY-27-SESSION.md
- Context window: 높음
