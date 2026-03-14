# PURPOSE-SCENARIO

## Ultimate Purpose

온라인 게임(MMORPG 등)의 TCP 패킷을 캡처하고, JSON 프로토콜 정의에 따라 파싱하여 재현 가능한 로그를 생성하고, 이를 활용해 QA 자동화(부하 테스트, 회귀 테스트)를 수행하는 도구.

## Usage Scenarios

### Phase 1: 패킷 구조 분석 → JSON 프로토콜 생성
- 대상 게임 소스(예: mmorpg-simulator)를 분석하여 패킷 구조 JSON 생성
- Kiro가 소스 분석 시 활용할 context를 이 프로젝트에 포함

### Phase 2: 캡처 + 파싱 + 로그 기록
- 게임 클라이언트↔서버 TCP 패킷을 캡처
- JSON 프로토콜 정의로 동적 파싱 → 사람이 읽을 수 있는 로그 생성

### Phase 3: 시나리오 자동 조립 + 수동 튜닝
- 캡처된 패킷 시퀀스를 의미론적으로 분석
- 로그인 → 캐릭터 선택 → 게임 입장 → 기능 사용/이동 등 시나리오 자동 생성
- 수동 튜닝으로 시나리오 정교화

### Phase 4: 다중 클라이언트 부하/회귀 테스트
- 단일 시나리오 재현 → 다중 클라이언트 동시 실행
- 부하 테스트, 회귀 테스트 수행

## Acceptance Criteria
- mmorpg-simulator 대상으로 캡처 → 파싱 → 재현 전체 파이프라인 동작
- 시나리오 기반 다중 클라이언트 동시 재현 가능

## Related Project
- [mmorpg-simulator](../mmorpg-simulator/) — 검증용 mockup 게임 클라이언트/서버

## Last Confirmed
2026-03-11, Mickey 1
