# MICKEY-29-SESSION

## Checkpoint [3/5]

## Session Meta
- Type: Maintenance/Implementation/Planning
- Mickey: 29
- Date: 2026-04-24

## Session Goal
현재 구현 상태 분석 + A 그룹 대기 작업 완료 + keploy 기반 개선 계획 수립

## Purpose Alignment
- 기여 시나리오: Phase 4/5 — E2E 검증 완료, 실서비스 적용을 위한 로드맵 수립
- 이번 세션 범위: FsmExecutor 버그 수정, A 그룹 E2E 검증, 개선 계획 수립

## Previous Context
- M27: 목업 서버(방안 B: 상태 추적) 구현 완료. 테스트 227개 통과.
- M28: 유지보수 — 교훈 승격 (proxy-design.md, async-csharp.md), 아카이빙 완료.

## Current Tasks

### 1. 현재 구현 상태 분석 ✅
- [x] PURPOSE-SCENARIO 대비 Phase별 달성도 평가
- Phase 1~4 전체 완료, Phase 5 구현 완료/E2E 미검증 → 이번 세션에서 검증 완료

### 2. FsmExecutor 이중 연결 버그 수정 ✅
- [x] ExecuteAsync()에서 사전 TCP 연결 제거 | CC: 코드 수정 + 테스트 227개 통과
- 원인: ExecuteAsync()가 먼저 연결 → FSM 루프 connect 상태에서 또 연결 시도 (이중 연결)
- 수정: stream=null로 RunFsmLoopAsync 시작, connect 상태에서만 연결

### 3. A-1: BT → 목업 서버 E2E 검증 ✅ (간접)
- [x] 프록시 패스스루 구간에서 BT→목업 서버 정상 통신 확인 (로그인→캐릭터→이동/전투)

### 4. A-2: FSM → 목업 서버 E2E 검증 ✅ (간접)
- [x] 프록시 takeover 구간에서 FSM→목업 서버 직접 통신 확인 (move/attack 44스텝)

### 5. A-3: 프록시 모드 E2E 검증 ✅
- [x] 패스스루 양방향 중계 정상 | CC: CS_LOGIN~CS_ATTACK 전체 흐름
- [x] takeover 전환 정상 | CC: PacketObserver가 move 상태 감지, FSM 44스텝 실행
- [x] 인터셉터 동작 | CC: ProximityInterceptor NPC 방향 이동
- [x] 목업 서버 응답 정상 | CC: NPC_DEATH→NPC_SPAWN 사이클 포함

### 6. README에 목업 서버 사용법 추가 ✅
- [x] 한국어: 기능 목록 + 목업 서버 섹션 + 옵션 테이블 | CC: --build-mock, --mock 추가
- [x] 영어: 동일 구조 추가

### 7. keploy 기반 개선 계획 수립 ✅
- [x] 제안서 검토 + keploy 아키텍처 추가 분석
- [x] 암호화 파이프라인 Gap 분석 (실서비스 적용 전제조건)
- [x] 5-Phase 실행 계획 수립

## Progress
- Completed: 전체 완료
- InProgress: 없음
- Blocked: 없음

## Key Decisions
- FsmExecutor 이중 연결: ExecuteAsync()에서 사전 연결 제거 (connect 상태에 위임)
- keploy 방향: Keploy에 게임 프로토콜 넣기 ❌ → Keploy 아이디어를 선택적 적용 ✅
- 개선 우선순위: 암호화 파이프라인(Phase 1) → 커버리지(Phase 2) → CI/CD(Phase 3) → 메트릭(Phase 4) → Linux 캡처(Phase 5)
- 암호화 파이프라인을 최우선으로 올린 이유: 실서비스 게임은 거의 100% 암호화 사용, 이것 없이는 재현/BT/FSM/프록시 동작 불가

## Files Modified
- PacketCaptureAgent/FsmExecutor.cs (사전 연결 제거)
- README.md (목업 서버 사용법 추가 — 한국어/영어)

## Lessons Learned
- FsmBuilder가 생성하는 connect/disconnect는 가상 상태. ExecuteAsync()에서 별도 연결하면 이중 연결 발생
- 암호화 파이프라인 Gap: IPacketTransform에 복호화(수신)만 있고 암호화(송신)가 없음. PacketBuilder에 Transform 미적용. 방향별 분리 없음. 프록시 모드에서도 미적용.

## Context Window Status
중간

## Next Steps (5-Phase 로드맵)
1. **Phase 1: 암호화 파이프라인 완성** (2~3 세션) — IPacketTransform에 ReverseTransform 추가, 방향별 Transform, PacketBuilder 암호화, 프록시 Transform 통합, 커스텀 플러그인 구조
2. **Phase 2: 커버리지 리포팅** (1~2 세션) — 패킷/FSM/BT 커버리지, --coverage 옵션
3. **Phase 3: CI/CD 통합** (1 세션) — GitHub Actions, 목업 서버 활용
4. **Phase 4: 부하 테스트 메트릭** (선택적) — TPS, 레이턴시, 에러율 리포트
5. **Phase 5: Linux 캡처** (장기) — SharpPcap/libpcap, cross-platform
