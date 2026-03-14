# Code Observations

## 잠재적 이슈

### 배열 파싱 count_field vs length 불일치
- `PacketParser`는 `count_field` 기반 동적 카운트 지원
- 그러나 `mmorpg_simulator.json`의 SC_CHAR_LIST chars 배열은 `"length": 5` 고정
- `count` 필드(uint8)가 별도로 있지만 `count_field`로 연결되지 않음
- 결과: count=0이어도 항상 5개 엔트리를 파싱 시도 → 빈 데이터 출력

### 미사용 코드
- `PacketReplayer.Replay()` — Program.cs에서 `ReplayWithParsing()`만 호출
- `PacketFormatter.FormatJson()` — 어디서도 호출되지 않음
- `Program.ShowUsage()` — 어디서도 호출되지 않음 (--help 미구현)

### Program.cs 구조
- 294줄, 캡처 로직이 static 메서드로 모두 포함
- 캡처 모드에서 `--port` CLI 인자 미지원 (인터랙티브 입력만)
- README에는 `--port` 옵션이 문서화되어 있으나 실제 구현 없음

### 재현 모드 제한
- `ReplayWithParsing()`은 SEND 패킷만 순회, 시간 간격 미적용
- 응답 대기가 `Thread.Sleep(50)` + `DataAvailable` 폴링 방식
- 단일 TCP 연결, 연결 실패 시 재시도 없음

### 빌드 경고
- CA1416: `IOControlCode.ReceiveAll` Windows 전용 — 의도된 동작이므로 suppress 가능
