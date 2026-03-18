# 패킷 분석 노트

## 노이즈 분류 (mmorpg_simulator 기준)

### 리플레이 제외 + 데이터 소스
- SC_NPC_SPAWN: npcUid, posX, posY (공격 대상 파악용)
- SC_NPC_DEATH: npcUid, expReward

### 리플레이 제외 (순수 노이즈)
- SC_CHAR_INFO (타인): charUid ≠ myCharUid
- SC_CHAR_LEAVE: 항상 타인
- SC_ATTENDANCE_INFO: 입장 시 서버 푸시
- SC_EXP_UPDATE, SC_LEVEL_UP: 결과 알림
- CS/SC_HEARTBEAT: 연결 유지

## 다른 플레이어 패킷 구분

SC_CHAR_INFO 이중 용도:
- CS_CHAR_SELECT 응답 → 내 캐릭터 (charUid == myCharUid)
- 비요청 브로드캐스트 → 다른 플레이어 (charUid ≠ myCharUid)

서버 브로드캐스트 지점 (GameServer/main.cpp):
- line 99: BroadcastExcept SC_CHAR_INFO (입장)
- line 393: BroadcastExcept SC_CHAR_INFO (이동)
- TcpServer.cpp:46: BroadcastExcept SC_CHAR_LEAVE (퇴장)

## 동적 필드 추적

| 패킷.필드 | 소스 | 비고 |
|-----------|------|------|
| CS_LOGIN.accountId | csv | 부하테스트 시 계정별 할당 |
| CS_LOGIN.password | csv | |
| CS_CHAR_SELECT.charUid | SC_CHAR_LIST.chars[0].charUid | 응답값 |
| CS_ATTACK.targetUid | SC_NPC_SPAWN.npcUid | 노이즈지만 데이터 소스 |
| CS_MOVE.dirX/dirY | static | 캡처 값 그대로 |
