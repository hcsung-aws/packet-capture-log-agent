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

#### Step 1: 단일 프로세스 동기 방식 ✅
- `--clients N` 옵션으로 N개 클라이언트 동시 실행
- Task.Run + ThreadPool, 클라이언트별 독립 TCP 연결
- 각 클라이언트 고유 랜덤 값 ({random:N})
- 결과 요약 (성공/실패, 패킷 수, 소요시간)
- 실용 범위: ~100-200 동시 클라이언트

#### Step 2: async 전환 ✅
- Thread.Sleep → Task.Delay, Replay → async
- ThreadPool 블로킹 제거 → 수천 동시 클라이언트 가능
- 기존 인터페이스 유지, 내부 구현만 변경

#### Step 3: 멀티 에이전트 매니저 ✅
- 매니저 어플리케이션이 여러 인스턴스에 에이전트를 배포/실행
- 에이전트별 클라이언트 수 할당, 결과 수집/집계
- 수만 동시 클라이언트 규모 부하 테스트

### Phase 5: 프록시 모드 (클라이언트 연동 테스트)
- 클라이언트↔서버 사이에 프록시로 개입
- 패스스루: 양방향 중계 + 패킷 파싱 + FSM/BT 상태 동기화
- Takeover: 클라이언트 인증/암호화 활용 후 원하는 시점에 자동 테스트 전환
- 클라이언트가 로그인까지 처리 → 이후 FSM/BT가 현재 상태에서 자동 실행

### Phase 6: 실제 서비스 게임 적용
- 암호화 파이프라인 완성 (IPacketTransform 양방향 + PacketBuilder 암호화 + 프록시 통합)
- 실서비스 게임의 암호화된 패킷을 캡처/파싱/재현 가능하게 확장
- 외부에서 제공받은 암호화 코드를 플러그인으로 통합
- 커버리지 리포팅, CI/CD 통합, 부하 테스트 메트릭 등 운영 품질 강화

## Acceptance Criteria
- mmorpg-simulator 대상으로 캡처 → 파싱 → 재현 전체 파이프라인 동작
- 시나리오 기반 다중 클라이언트 동시 재현 가능 (Phase 4 Step 1) ✅
- 프록시 모드로 클라이언트 연동 takeover 테스트 가능 (Phase 5, E2E 검증 필요)
- 암호화된 패킷의 캡처/파싱/재현이 가능하고, 실서비스 게임에 적용 가능 (Phase 6)

## Related Project
- [mmorpg-simulator](../mmorpg-simulator/) — 검증용 mockup 게임 클라이언트/서버

## Last Confirmed
2026-04-24, Mickey 30
