# TestPlay 설계: Behavior Tree 기반 게임 재현 테스트

## 배경

| 모드 | 목적 | 특성 |
|------|------|------|
| **Replay (기존)** | 기능 회귀 테스트 | 녹화된 패킷 그대로 재전송. 서버 상태 무관. |
| **TestPlay (신규)** | 부하/시나리오 테스트 | 서버 응답에 따라 동적 판단. 조건 분기, 상태 추적. |

Replay는 "같은 입력 → 같은 출력?" 검증.
TestPlay는 "이 시나리오를 N명이 동시에 수행하면?" 검증.

## 아키텍처

```
PacketCaptureAgent/
├── Core/                        ← 공유 (기존)
│   ├── Protocol.cs              프로토콜 정의/로딩
│   ├── PacketParser.cs          수신 패킷 파싱
│   ├── PacketBuilder.cs         송신 패킷 빌드
│   ├── PacketFormatter.cs       출력 포맷
│   └── TcpStream.cs             TCP 스트림 재조립
│
├── Replay/                      ← 단순 재현 (기존)
│   ├── PacketReplayer.cs        코어 리플레이 루프
│   └── IResponseHandler.cs      응답 처리 전략
│
└── TestPlay/                    ← 신규
    ├── BehaviorTree/
    │   ├── BtNode.cs            노드 기본 타입 (Sequence, Selector, Action, Condition, Repeat)
    │   ├── BtStatus.cs          Success / Failure / Running
    │   └── BtRunner.cs          틱 루프 (노드 트리 순회)
    │
    ├── Actions/                 게임 액션 (BtNode 구현체)
    │   ├── SendPacketAction.cs  패킷 전송 (PacketBuilder 사용)
    │   ├── WaitPacketAction.cs  특정 패킷 대기 + 필드 추출
    │   ├── MoveToAction.cs      목표 좌표까지 이동 (CS_MOVE 반복)
    │   └── AttackAction.cs      대상 공격 (CS_ATTACK 반복, HP 체크)
    │
    ├── Conditions/              조건 판단
    │   ├── HasCharacterCondition.cs   캐릭터 보유 여부
    │   ├── IsAliveCondition.cs        HP > 0 체크
    │   └── NpcExistsCondition.cs      공격 가능 NPC 존재 여부
    │
    ├── GameState.cs             월드 상태 (내 위치, NPC 목록, 인벤토리 등)
    ├── GameConnection.cs        TCP 연결 + 패킷 송수신 (Core 사용)
    ├── ScenarioLoader.cs        JSON 시나리오 → BT 변환
    └── TestRunner.cs            다중 클라이언트 오케스트레이션
```

## 공유 컴포넌트 의존성

```
TestPlay 모듈이 Core에서 사용하는 것:
  - PacketBuilder.Build(name, fields, overrides)  ← 패킷 생성
  - PacketParser.TryParse(tcpStream)              ← 응답 파싱
  - ProtocolDefinition                            ← 패킷 정의 참조

TestPlay 모듈이 Core에서 사용하지 않는 것:
  - PacketReplayer (Replay 전용)
  - PacketFormatter (캡처 모드 전용)
```

## Behavior Tree 예시: "로그인 → 사냥" 시나리오

```
Root (Sequence)
│
├── Phase: Login (Sequence)
│   ├── [Action] SendPacket(CS_LOGIN, {accountId: $csv.id, password: $csv.pw})
│   ├── [Action] WaitPacket(SC_LOGIN_RESULT)
│   └── [Condition] Check(SC_LOGIN_RESULT.success == 1)
│
├── Phase: Character (Selector = 첫 성공 시 중단)
│   ├── SelectExisting (Sequence)
│   │   ├── [Action] WaitPacket(SC_CHAR_LIST)
│   │   ├── [Condition] Check(SC_CHAR_LIST.count > 0)
│   │   └── [Action] SendPacket(CS_CHAR_SELECT, {charUid: $state.charList[0].charUid})
│   │
│   └── CreateNew (Sequence)
│       ├── [Action] SendPacket(CS_CHAR_CREATE, {name: $csv.charName, charType: 1})
│       ├── [Action] WaitPacket(SC_CHAR_CREATE_RESULT)
│       └── [Action] SendPacket(CS_CHAR_SELECT, {charUid: $state.newCharUid})
│
├── Phase: EnterGame (Sequence)
│   ├── [Action] WaitPacket(SC_CHAR_INFO) → GameState에 위치/HP 저장
│   └── [Action] CollectSpawns(SC_NPC_SPAWN) → GameState에 NPC 목록 저장
│
└── Phase: Hunt (Repeat count=10)
    ├── [Condition] NpcExists()
    ├── [Action] FindNearestNpc() → targetUid, targetPos
    ├── [Action] MoveTo(targetPos)  ← 맨해튼 거리 기반 CS_MOVE 반복
    └── [Action] AttackUntilDead(targetUid) (Repeat until targetHp == 0)
        ├── [Action] SendPacket(CS_ATTACK, {targetUid: $state.targetUid})
        └── [Action] WaitPacket(SC_ATTACK_RESULT)
```

## GameState 설계

```csharp
public class GameState
{
    // 내 정보
    public ulong MyCharUid { get; set; }
    public (int x, int y) MyPos { get; set; }
    public int MyHp { get; set; }

    // 월드 정보
    public Dictionary<ulong, NpcInfo> Npcs { get; }  // npcUid → (pos, hp)
    public Dictionary<ulong, PlayerInfo> OtherPlayers { get; }

    // 서버 응답 임시 저장 (WaitPacket 결과)
    public Dictionary<string, object> LastResponse { get; }

    // 수신 패킷 자동 반영
    public void OnPacketReceived(ParsedPacket packet) {
        // SC_NPC_SPAWN → Npcs에 추가
        // SC_NPC_DEATH → Npcs에서 제거
        // SC_MOVE_RESULT → MyPos 갱신
        // SC_CHAR_INFO(타인) → OtherPlayers 갱신
        // SC_CHAR_LEAVE → OtherPlayers에서 제거
    }
}
```

## 이동 로직 (MoveToAction)

길찾기는 현재 mmorpg_simulator의 맵이 20x20 격자 + 장애물 없음이므로:
- **Phase 1**: 맨해튼 거리 기반 직선 이동 (dx, dy 방향으로 CS_MOVE 반복)
- **Phase 2 (필요 시)**: A* 또는 BFS 길찾기 (장애물 맵 추가 시)

```
MoveTo(targetX, targetY):
  while (myPos != target):
    dx = sign(targetX - myX)  // -1, 0, 1
    dy = sign(targetY - myY)
    SendPacket(CS_MOVE, {dirX: dx, dirY: dy})
    WaitPacket(SC_MOVE_RESULT)
    UpdateMyPos()
```

## 시나리오 JSON 형식 (ScenarioLoader 입력)

```json
{
  "name": "login_and_hunt",
  "description": "로그인 → 캐릭터 선택/생성 → 사냥 10회",
  "csv_fields": ["accountId", "password"],
  "tree": {
    "type": "sequence",
    "children": [
      {
        "type": "action",
        "action": "send_packet",
        "params": {"packet": "CS_LOGIN", "fields": {"accountId": "$csv.accountId", "password": "$csv.password"}}
      },
      {
        "type": "action",
        "action": "wait_packet",
        "params": {"packet": "SC_LOGIN_RESULT", "store": {"accountUid": "state.accountUid"}}
      }
    ]
  }
}
```

## 다중 클라이언트 (TestRunner)

```
TestRunner:
  Input: scenario.json + accounts.csv + concurrency=100
  
  1. CSV에서 계정 N개 로드
  2. 각 계정마다 독립 GameConnection + GameState + BT 인스턴스 생성
  3. Task.WhenAll()로 동시 실행
  4. 결과 집계: 성공/실패/평균 응답시간/에러 분포
```

## 구현 우선순위

| 순서 | 작업 | 의존성 |
|------|------|--------|
| 1 | BtNode 기본 타입 (Sequence, Selector, Action, Condition, Repeat) | 없음 |
| 2 | GameState + GameConnection (Core 의존) | Core |
| 3 | 기본 Actions (SendPacket, WaitPacket) | 1, 2 |
| 4 | 로그인→캐릭터 선택 시나리오 동작 검증 | 1, 2, 3 |
| 5 | MoveToAction (직선 이동) | 3 |
| 6 | AttackAction | 3 |
| 7 | ScenarioLoader (JSON → BT) | 1 |
| 8 | TestRunner (다중 클라이언트) | 전체 |

## 선행 작업

Phase 3 본격 구현 전 필요한 것:
1. **PacketFormatter 배열 출력 수정** — 로그에서 구조체 배열 필드 추출 가능하게
2. **노이즈 분류 세분화** — replay_skip(리플레이 제외) vs data_source(데이터 수집용) 구분
3. 위 2개는 기존 Replay 모드 개선이므로 TestPlay와 독립 진행 가능
