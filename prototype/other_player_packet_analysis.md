# 다른 플레이어 패킷 분석

## mmorpg_simulator 서버의 브로드캐스트 패킷

서버 소스(GameServer/main.cpp, TcpServer.cpp) 분석 결과:

### 다른 플레이어 관련 패킷 (3종)

| 패킷 | 발생 시점 | 브로드캐스트 방식 |
|------|----------|-----------------|
| **SC_CHAR_INFO** | 다른 플레이어 입장 시 (main.cpp:99) | `BroadcastExcept` — 본인 제외 전체 |
| **SC_CHAR_INFO** | 다른 플레이어 이동 시 (main.cpp:393) | `BroadcastExcept` — 본인 제외 전체 |
| **SC_CHAR_LEAVE** | 다른 플레이어 접속 종료 시 (TcpServer.cpp:46) | `BroadcastExcept` — 본인 제외 전체 |

### SC_CHAR_INFO의 이중 용도 문제

SC_CHAR_INFO는 두 가지 상황에서 수신됨:
1. **내 캐릭터 정보** — CS_CHAR_SELECT 직후 응답 (main.cpp:73)
2. **다른 플레이어 정보** — 비요청 브로드캐스트 (main.cpp:87, 99, 393)

### 구분 방법

| 방법 | 설명 | 신뢰도 |
|------|------|--------|
| **타이밍 기반** | CS_CHAR_SELECT 직후 수신 → 내 것, 비요청 → 타인 | 중 (네트워크 지연 시 모호) |
| **charUid 매칭** | CS_CHAR_SELECT에서 보낸 charUid와 일치 → 내 것 | 높 |
| **순서 기반** | CS_CHAR_SELECT 후 첫 SC_CHAR_INFO → 내 것, 이후 → 타인 | 높 (현재 프로토콜 기준) |

**권장**: charUid 매칭 방식. SessionState에 내 charUid를 저장하고, SC_CHAR_INFO 수신 시 비교.

### 전체 브로드캐스트 패킷 (노이즈 후보)

| 패킷 | 대상 | 필터링 | 데이터 소스 역할 |
|------|------|--------|----------------|
| SC_NPC_SPAWN | 전체 (`Broadcast`) | 리플레이 제외 | ✅ npcUid, posX, posY |
| SC_NPC_DEATH | 전체 (`Broadcast`) | 리플레이 제외 | ✅ npcUid, expReward |
| SC_CHAR_INFO (타인) | 본인 제외 | 리플레이 제외 | ❌ |
| SC_CHAR_LEAVE | 본인 제외 | 리플레이 제외 | ❌ |
| SC_ATTENDANCE_INFO | 본인만 (입장 시) | 리플레이 제외 | ❌ |
| SC_EXP_UPDATE | 본인만 | 리플레이 제외 | ❌ |
| SC_LEVEL_UP | 본인만 | 리플레이 제외 | ❌ |

### 현재 캡처 로그 상태

capture_20260313_231631.log에는 다른 플레이어 관련 패킷 없음 (단일 클라이언트 테스트).
멀티 클라이언트 테스트 시 SC_CHAR_INFO(타인), SC_CHAR_LEAVE가 추가로 수신될 것.

### 필터링 구현 시 필요 사항

1. SessionState에 `myCharUid` 저장 (CS_CHAR_SELECT 시점)
2. SC_CHAR_INFO 수신 시: `charUid == myCharUid` → 내 정보, 아니면 → 타인 (노이즈)
3. SC_CHAR_LEAVE → 항상 노이즈 (리플레이 불필요)
