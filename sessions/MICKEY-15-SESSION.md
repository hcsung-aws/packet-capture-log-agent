# MICKEY-15-SESSION

## Session Meta
- Type: Implementation
- Date: 2026-03-27~28

## Session Goal
mmorpg_simulator 퀘스트/파티 시스템 구현 + BT 자동 생성까지

## Purpose Alignment
- 기여 시나리오: Phase 2 (BT 테스트용 기능 추가) + Phase 3 (BT 자동 생성)
- 이번 세션 범위: 퀘스트/파티 구현 → 캡처 검증 → BT 생성

## Previous Context
Mickey 14: Phase 4 Step 1 완료 + BT 단계 A~E 완료.

## Current Tasks
- [x] Protocol.h: 0x0Axx 퀘스트 패킷 + 구조체
- [x] data/quests.json + scripts/quest_system.sql
- [x] GameServer/main.cpp: 퀘스트 로직
- [x] GameClient/main.cpp: 퀘스트 UI (J키, 트래커)
- [x] 퀘스트 빌드 + E2E 캡처 검증
- [x] Protocol.h: 0x0Bxx 파티 패킷 + 구조체
- [x] GameServer/main.cpp: 파티 로직 (초대/수락/탈퇴, EXP분배, 파티채팅)
- [x] GameClient/main.cpp: 파티 UI (P키, Y/N, HUD, /p)
- [x] 파티 빌드 + E2E 캡처 검증
- [x] protocols/mmorpg_simulator.json 동기화 (총 50패킷)
- [x] BT 자동 생성 (--build-behavior, 7건 녹화 → mmorpg_simulator_auto.json)

## Key Decisions
- 퀘스트: 킬 퀘스트만, Quest NPC 고정(2,2), 인터셉터 트리거=CS_QUEST_LIST
- 파티: 최대 4인, 메모리 전용(DB 불필요), 파티장 탈퇴 시 해산
- Broadcast에 loggedIn 체크 추가 (미입장 세션에 게임 패킷 전송 방지)
- OnMove 브로드캐스트에 name/level/hp 포함하도록 수정
- OnShopBuy 실패 시 remainGold에 현재 gold 반환하도록 수정
- 멀티 클라이언트 캡처: 127.0.0.1(loopback, 캡처 안됨) + 172.22.32.1(캡처됨) 분리 전략

## Progress

### Completed
- 퀘스트 시스템 전체 (Protocol + Server + Client + DB + JSON)
- 파티 시스템 전체 (Protocol + Server + Client + JSON)
- 버그 수정 3건 (Broadcast loggedIn, OnMove name, ShopBuy gold)
- BT 자동 생성 완료 (7건 녹화 → behaviors/mmorpg_simulator_auto.json)

### InProgress

### Blocked

## Files Modified
- mmorpg_simulator: Protocol.h, GameServer/main.cpp, GameClient/main.cpp, TcpServer.cpp, SessionManager.cpp
- mmorpg_simulator: data/quests.json, scripts/quest_system.sql
- packet-capture-log-agent: protocols/mmorpg_simulator.json

## Lessons Learned
- Broadcast는 반드시 loggedIn 체크 필요 — 미입장 세션에 게임 패킷 보내면 클라이언트 상태 오염
- OnMove 등 브로드캐스트 시 SC_CharInfo 필드 누락 주의 — posX/posY만 채우면 name/hp=0
- ShopBuy 실패 시에도 remainGold 반환 필수 — 클라이언트가 success 체크 없이 gold 덮어씀
- 멀티 클라이언트 캡처: loopback(127.0.0.1) 활용하면 특정 클라이언트만 캡처 가능
- BT 자동 생성 조건은 accountUid 등 세션 고유값에 의존 — 수동 편집 필수

## Context Window Status
~25%

## Next Steps
- BT 조건 수동 편집 (accountUid → 게임 상태 기반)
- BT 실행 E2E 검증 (--behavior 옵션으로 서버 대상)
