# MICKEY-5-HANDOFF

## Current Status
Mickey 3 TODO 전부 소화 + Phase 3 프로토타이핑/설계 완료 + mmorpg_simulator 기능 추가 계획 수립.

## Next Steps
mmorpg_simulator에 채팅 기능 구현 (FEATURE_PLAN.md §1) → 인벤토리/아이템 → 상점 → 파티 순서.

## Important Context
- mmorpg_simulator는 C++ 프로젝트 (boost::asio, ODBC). 경로: ../mmorpg_simulator/
- FEATURE_PLAN.md에 패킷 타입 번호 체계(0x06xx=Chat 등), 필드 구조, 의존 관계 정리됨
- Phase 3(TestPlay BT 설계)는 prototype/ 디렉토리에 보류 중 — mmorpg_simulator 기능 보강 후 진행
- packet-capture-log-agent 리팩토링 완료: IResponseHandler 기반 Replay, --help 연결, count_field 수정

## Quick Reference
- SESSION: MICKEY-5-SESSION.md
- auto_notes: NOTES.md (packet-analysis.md 신규)
- Phase 3 설계: prototype/testplay_design.md, prototype/PHASE3_PLAN.md
- 기능 계획: ../mmorpg_simulator/FEATURE_PLAN.md
- Context window: 정상
