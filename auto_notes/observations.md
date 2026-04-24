# Code Observations

## 잠재적 이슈

### 미사용 코드
- `TcpStreamManager.Cleanup()` — 어디서도 호출되지 않음

### 재현 모드 제한
- 응답 수신이 SEND 당 1회 `stream.Read()`만 호출 — 서버가 연속 응답 시 일부 누락 가능
- 단일 TCP 연결, 연결 실패 시 재시도 없음

### ParseLog 배열 필드 — 해결됨 (Mickey 12)
- PacketFormatter가 배열/구조체를 flat key로 출력 (chars[0].charUid 등)
- ParseLog regex 확장으로 flat key 추출 정상 동작

### NPC 공격 인터셉터 — 보류된 개선 사항
- NPC가 예상보다 먼저/늦게 죽는 경우 공격 시퀀스 불일치 발생 가능
- 보류 사유: 현재 리플레이는 완전한 재현이 아닌 파이프라인 검증 수준 (Mickey 9)
- 재검토 시점: 에이전트가 상태 기반 동적 판단으로 고도화될 때

### 빌드 경고
- CA1416: `IOControlCode.ReceiveAll` Windows 전용 — 의도된 동작이므로 suppress 가능

### mmorpg_simulator MAX_INVENTORY 통합
- Protocol.h에 MAX_INVENTORY 정의 통합 (GameServer/GameClient 로컬 정의 제거, Mickey 11)
- SC_INVENTORY_LIST 추가로 초기 인벤토리 1회 전송 (기존 개별 SC_INVENTORY_UPDATE ×N → 1패킷)

### AgentCore 아키텍처 관찰 (Mickey 20)
- HTTP API v2 payload format 2.0은 v1과 이벤트 구조가 다름 (httpMethod→requestContext.http.method 등)
- HTTP API v2는 네이티브 API Key 미지원 → Lambda authorizer로 구현
- Orchestrator가 source.zip을 언팩하여 개별 파일로 S3 업로드하는 구조
- Program.cs 호출부는 .GetAwaiter().GetResult()로 동기 래핑 중 — Main async 전환 시 제거 가능

### AgentCore 파이프라인 개선 (Mickey 22)
- Discovery가 missing_dependencies를 감지하지만 CLI에 경고 미표시 → 개선 필요
- Generation을 결정론적 코드로 전환: 실행 시간 ~100초→<1초, count_field 100% 정확
- packet_handler 역할을 Analysis 대상에 추가하면 struct 없이 직접 읽는 필드도 추출 가능

## Step Functions 실패 시 output
- FAILED 상태에서 describe_execution의 output은 빈 객체 `{}`
- 중간 단계 결과(discovery.json 등)는 S3에서 직접 읽어야 함
- orchestrator _get_status에서 S3 discovery.json 읽어 warnings에 포함하도록 구현됨

### PacketBuilder 배열 직렬화 (Mickey 27)
- GetList()는 `List<object>`만 인식. `List<Dictionary<string,object>>`는 빈 리스트로 처리됨
- 배열 데이터 전달 시 `(object)` 캐스트 필수: `.Select(x => (object)new Dictionary<...>{...}).ToList()`

### 동작 시나리오 확인 시 데이터 타입 호환성 (Mickey 27)
- 시나리오 확인(T1.5 §10)에서 동작 흐름/연결점/사용법을 검토했지만 배열 직렬화 버그를 사전에 못 잡음
- 시나리오 확인 시 "데이터가 어떤 타입으로 전달되어 기존 코드가 처리할 수 있는가"까지 검토 필요

### FsmExecutor 이중 연결 (Mickey 29)
- FsmBuilder가 생성하는 connect/disconnect는 가상 상태 (실제 액션이 아님)
- ExecuteAsync()에서 사전 TCP 연결 후 FSM 루프의 connect 상태에서 또 연결 → 이중 연결 버그
- 수정: ExecuteAsync()에서 사전 연결 제거, connect 상태에서만 연결

### 암호화 파이프라인 Gap (Mickey 29)
- IPacketTransform: 복호화(수신)만 구현, 암호화(송신) 없음
- PacketBuilder: Transform 미적용 → 재현/BT/FSM이 평문 패킷 전송
- 방향별 Transform 분리 없음 (C2S/S2C 다른 암호화 시 대응 불가)
- 프록시 모드: raw 바이트 중계, 암호화 패킷 파싱/재암호화 미지원
- 실서비스 게임 적용 시 필수 해결 대상
