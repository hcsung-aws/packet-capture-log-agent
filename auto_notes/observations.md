# Code Observations

## 잠재적 이슈

### 배열 파싱 count_field vs length 불일치
- ~~`mmorpg_simulator.json`의 SC_CHAR_LIST chars 배열は `"length": 5` 고정~~ → 수정 완료 (`count_field: "count"` 연결됨, Mickey 9 확인)

### 미사용 코드
- `PacketFormatter.FormatJson()` — 어디서도 호출되지 않음 (이미 삭제됨, 확인 완료 Mickey 9)
- `TcpStreamManager.Cleanup()` — 어디서도 호출되지 않음

### Program.cs 구조
- 캡처 로직이 static 메서드로 포함 (인자 파싱은 ParseArgs로 분리됨, Mickey 9)

### 재현 모드 제한
- 응답 수신이 SEND 당 1회 `stream.Read()`만 호출 — 서버가 연속 응답 시 일부 누락 가능
- 단일 TCP 연결, 연결 실패 시 재시도 없음

### NPC 공격 인터셉터 — 보류된 개선 사항
- NPC가 예상보다 먼저/늦게 죽는 경우 공격 시퀀스 불일치 발생 가능
- 보류 사유: 현재 리플레이는 완전한 재현이 아닌 파이프라인 검증 수준 (Mickey 9)
- 재검토 시점: 에이전트가 상태 기반 동적 판단으로 고도화될 때

### 빌드 경고
- CA1416: `IOControlCode.ReceiveAll` Windows 전용 — 의도된 동작이므로 suppress 가능
