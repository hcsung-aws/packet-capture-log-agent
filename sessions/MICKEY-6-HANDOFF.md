# MICKEY-6-HANDOFF

## Current Status
채팅 기능 완료(테스트 통과), 인벤토리/아이템 코드 구현 완료(기존 DB 테이블 호환), 빌드/테스트 검증 대기.

## Next Steps
인벤토리 빌드 테스트 → 상점 기능 (FEATURE_PLAN §3) → 파티 (§4).

## Important Context
- mmorpg_simulator 콘솔 I/O: _getch()/_getwch() 통일, std::cin 혼용 금지 (auto_notes/windows-console.md)
- 인벤토리 DB: 기존 inventory(ItemUid/ItemTid) + equipment(Slot) 테이블 활용, 슬롯은 서버 메모리
- 배포: run_server.bat/run_client.bat (프로젝트 루트에서 실행), deploy.bat (패키징)
- VS 2026 환경: v143 개별 구성 요소 설치 필요

## Quick Reference
- SESSION: MICKEY-6-SESSION.md
- auto_notes: windows-console.md (신규), commands.md (mmorpg_simulator 추가)
- common_knowledge: windows-console-io.md (신규)
- 기능 계획: ../mmorpg_simulator/FEATURE_PLAN.md
- Context window: 정상
