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
