# MICKEY-27-SESSION

## Session Meta
- Type: Implementation
- Mickey: 27
- Date: 2026-04-14

## Session Goal
목업 서버 구현 (방안 B: 상태 추적)

## Purpose Alignment
- 기여 시나리오: QA 자동화 확장 — 실제 서버 없이 BT/FSM 테스트 가능한 목업 서버
- 이번 세션 범위: MockRule + MockRuleBuilder + MockServer + MockServerMode 구현

## Previous Context
- MICKEY-26: 목업 서버 설계 완료 (방안 A vs B 비교). 사용자가 방안 B(상태 추적) 선택.

## Current Tasks

### 1. MockRule 모델 + MockSession 상태
- [x] MockRule.cs: 규칙 모델 + MockSession(세션별 상태) | CC: 빌드 성공 ✅

### 2. MockRuleBuilder
- [x] MockRuleBuilder.cs: ActionCatalog → mock_rules.json 자동 생성 | CC: 빌드 성공 ✅

### 3. MockServer
- [x] MockServer.cs: TCP 서버 + 세션 관리 + 상태 기반 응답 | CC: 빌드 성공 ✅

### 4. MockServerMode CLI
- [x] MockServerMode.cs + Program.cs 연결 | CC: 빌드 성공 + --build-mock/--mock 옵션 동작 ✅

### 5. 테스트
- [x] MockServer 단위 테스트 | CC: 227개 전체 통과 (기존 212 + 신규 15) ✅

## Progress
- Completed: MockRule, MockRuleBuilder, MockServer, MockServerMode, 테스트 15개, 배열 직렬화 버그 수정, field_ranges 도입
- InProgress: 없음
- Blocked: 없음

## Key Decisions
1. **방안 B 선택**: 사용자가 방안 A(정적)는 데모 수준 미달로 판단, B(상태 추적) 직행
2. **1차 상태 추적 범위**: 이동/전투/상점/인벤토리 = 동적, 퀘스트/파티/채팅 = 고정 응답
3. **field_ranges 도입**: recordings의 recv_state에서 S2C 필드별 min/max 추출 → 스폰 좌표/HP 등에 사용. recordings 없으면 기본값(0~19, HP 30)
4. **이동 경계 제한은 수정하지 않기로**: field_ranges의 관측 범위를 이동 clamp에 사용하면 맵 끝까지 안 간 녹화에서 경계가 좁아지는 문제 발견. 관측 범위는 스폰용이지 맵 경계가 아니므로 현 상태 유지

## Files Modified
- 신규: MockRule.cs, MockRuleBuilder.cs, MockServer.cs, MockServerMode.cs
- 신규: MockServerTests.cs (15개 테스트)
- 변경: Program.cs (--build-mock, --mock 옵션 추가)

## Lessons Learned
1. PacketBuilder의 GetList()는 List<object>만 인식 — List<Dictionary<string,object>>는 빈 리스트로 처리됨. 배열 데이터 전달 시 (object) 캐스트 필요
2. field_ranges(관측 범위)를 이동 경계 제한에 사용하면 안 됨 — 녹화 중 맵 끝까지 안 갔을 수 있으므로 관측 범위 ≠ 맵 경계
3. [Protocol] 동작 시나리오 확인(T1.5 §10)이 배열 직렬화 버그를 사전에 잡지 못함 — 시나리오 확인 시 데이터 타입 호환성까지 검토 필요

## Context Window Status
높음

## Next Steps
- E2E 테스트: BT/FSM → 목업 서버 접속 검증
- 퀘스트/파티 상태 추적 확장 (필요 시)
- README에 목업 서버 사용법 추가
