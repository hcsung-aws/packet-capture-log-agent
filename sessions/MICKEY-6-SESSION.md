# MICKEY-6-SESSION

## Session Goal
mmorpg_simulator 채팅 + 인벤토리/아이템 기능 구현 (FEATURE_PLAN §1, §2)

## Previous Context
Mickey 5: 리플레이 리팩토링(IResponseHandler), Phase 3 BT 설계, mmorpg_simulator 기능 추가 계획(채팅→인벤토리→상점→파티).

## Current Tasks
- [x] 채팅 기능 구현 | CC: World Broadcast + Whisper, 빌드/테스트 통과
- [x] 채팅 버그 수정 3건 | CC: DrawMap 이중호출, stdin/_getch 혼용, 게임입장시 채팅진입
- [x] UTF-8 인코딩 통일 | CC: 서버/클라 SetConsoleOutputCP+setlocale, 클라 _getwch()+WideCharToMultiByte
- [x] 인벤토리/아이템 코드 구현 | CC: items.json, DB SP, 패킷6종, 서버/클라 핸들러
- [x] 기존 DB 테이블 호환 수정 | CC: ItemUid/ItemTid 기반, soft delete
- [x] 배포 구조 정리 | CC: run_server/client.bat, deploy.bat

## Key Decisions
- 인코딩: 전체 UTF-8 통일 (Linux 포팅 대비)
- 콘솔 입력: _getch()/_getwch() 통일 (std::cin 혼용 금지)
- 아이템 데이터: JSON 파일 (data/items.json)
- 인벤토리: 기존 DB 테이블(inventory/equipment) 활용, 슬롯은 서버 메모리
- 아이템 사용 효과: broadcast (SC_ITEM_USE_RESULT)
- 배포: exe+data/ 동일 레벨, run_*.bat으로 개발 실행

## Files Modified
- Common/Protocol.h, GameServer/Session.h, GameServer/main.cpp, GameClient/main.cpp
- data/items.json, scripts/inventory_system.sql
- protocols/mmorpg_simulator.json
- run_server.bat, run_client.bat, deploy.bat

## Lessons Learned
- Windows _getch()(conio.h)와 std::cin(iostream) 혼용 시 콘솔 입력 버퍼 불일치 → 한쪽 통일 필수
- SetConsoleOutputCP(65001)만으로는 std::cout UTF-8 출력 불가 → setlocale(LC_ALL, ".UTF-8") 병행 필요
- 기존 DB 테이블 존재 시 CREATE TABLE IF NOT EXISTS는 스키마 불일치를 숨김 → 먼저 DESCRIBE로 확인
- PostBuild 복사보다 실행 bat 파일이 유지보수 용이 (사용자 피드백)

## Context Window Status
정상

## Next Steps
- 인벤토리 빌드/테스트 검증
- 상점 기능 (FEATURE_PLAN §3)
