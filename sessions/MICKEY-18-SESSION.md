# MICKEY-18-SESSION

## Session Meta
- Type: Maintenance
- Date: 2026-04-03~04

## Session Goal
엔트로피 체크 처리 + field_variants 검증 + explore phase 자동 생성 + 프로젝트 전체 평가 + 작업 로드맵 정리

## Purpose Alignment
- 기여 시나리오: Infrastructure (유지보수) + Phase 3 (BT 개선)
- 이번 세션 범위: 세션 아카이빙, 교훈 승격, 경량 포스트모템, BT explore phase, 전체 평가

## Previous Context
Mickey 17: 범용화 3단계 완료 (BT Builder 범용화 + 타입 확장 + AgentCore + field_variants). 119개 테스트 통과.

## Current Tasks
- [x] 교훈 승격 리뷰 (MICKEY-12~14) | CC: 승격 1건(sequence-analysis.md) 사용자 확인 완료
- [x] MICKEY-12~14 아카이빙 → sessions/ | CC: 6파일 이동, 루트에 15~17 남음
- [x] 경량 포스트모템 (17세션) | CC: [Protocol] 태그 8건 수집, 긍정 4/부정 1, 미반영 1건 처리
- [x] 세션 로그 기록 품질 규칙 반영 | CC: T1(시스템 프롬프트 During Session) + T1.5(§13) + ai-developer-mickey push
- [x] field_variants E2E 검증 | CC: CS_MOVE dirY=-1(이전 고정값 0과 다름), CS_ITEM_EQUIP slot=8, CS_SHOP_BUY itemId=4 확인
- [x] BT explore phase 자동 생성 | CC: InjectExplorePhases 구현, Repeat x6 × move 삽입 확인, 119개 테스트 통과
- [x] 프로젝트 전체 평가 + 작업 로드맵 정리 | CC: PURPOSE-SCENARIO 대비 Phase별 달성도 평가, A/B/C 3단계 로드맵 확정

## Progress

### Completed
- 교훈 승격: 동적 필드 감지 규칙(suffix 타입 필터 + 시간 순서)을 context_rule/sequence-analysis.md에 추가
- 아카이빙: MICKEY-12~14 (6파일) → sessions/
- 경량 포스트모템: [Protocol] 태그 수집 + 분류. 미반영 사항 "세션 로그 기록 품질" → T1.5 §13 신설 + T1 During Session에 규칙 추가. Version 8→9
- ai-developer-mickey push 완료 (e794258)
- field_variants 검증: BT validation 모드에서 move/item_equip/shop_buy 모두 랜덤 선택 확인
- BT explore phase: BehaviorTreeBuilder에 InjectExplorePhases 구현. field_variants 2종 이상 + 비상호작용 액션 → 해당 시퀀스 선두에 repeat(min(maxVariants*2, 10)) 삽입. validation 모드에서는 액션 타입당 1회 실행이라 repeat 전체는 duration 모드에서 확인 필요
- 전체 평가: Acceptance Criteria 2/2 충족. Phase 1~3 + 4-1 완료, 4-2/4-3 미구현

## Key Decisions
- 세션 로그 기록 품질 규칙: adaptive.md(프로젝트별)가 아닌 T1.5(배포 대상)로 승격 — 모든 프로젝트에 처음부터 적용되어야 하는 범용 규칙이므로
- explore phase: 카탈로그의 field_variants + semantics의 interactionSources로 탐색 후보 자동 식별. 별도 옵션 없이 --build-behavior에 포함
- 작업 로드맵: A(품질) → B(AgentCore) → C(스케일링) 순서 확정

## Files Modified
- context_rule/sequence-analysis.md (동적 필드 감지 규칙 추가)
- PacketCaptureAgent/BehaviorTreeBuilder.cs (InjectExplorePhases + InsertExploreNode)
- ~/.kiro/mickey/extended-protocols.md (§13 세션 로그 기록 품질)
- ~/ai-developer-mickey/examples/ai-developer-mickey.json (T1 During Session 규칙 + Version 9)
- ~/ai-developer-mickey/mickey/extended-protocols.md (T1.5 §13 + Version 9)

## Lessons Learned
- BT validation 모드는 각 액션 타입을 1회만 실행 → repeat 노드의 반복 효과는 duration 모드에서만 확인 가능
- 프로토콜 개선(T1/T1.5)은 adaptive.md(프로젝트별)가 아닌 배포 대상 파일에 반영해야 새 프로젝트에 전파됨

## Context Window Status
~40%

## Next Steps
아래 로드맵 순서대로 진행:

### A. 품질 개선 + 코드 리뷰
1. auto_notes/status.md 최신화
2. 코드 리뷰 — 구조/중복/네이밍/에러 처리 개선점 식별 + 수정
3. BT Builder/Executor 단위 테스트 추가

### B. AgentCore 개선 (다른 게임 적용 대비)
4. Discovery 파일 선별 개선 — 유틸리티/생성 스크립트(generate_protocol.py 등)를 relevant_files에서 제외. Discovery 프롬프트에 "코드를 생성하는 스크립트는 제외, 실제 패킷 구조를 정의하는 파일만 포함" 지침 추가 또는 role 기반 후처리 필터
5. API Gateway 인증 추가 (IAM 또는 API Key — 현재 인증 없이 공개 상태)
6. 모델 비교 (Haiku/Sonnet 3.7 vs Sonnet 4 — 동일 소스로 생성 품질 + 비용 + 시간 비교)

### C. 스케일링 대비
7. Phase 4 Step 2: async 전환 — Thread.Sleep→Task.Delay, PacketReplayer/ActionExecutor 내부 async화. 인터페이스 유지, 수천 동시 클라이언트 대응
8. Phase 4 Step 3: 멀티 에이전트 매니저 — 매니저 앱이 여러 인스턴스에 에이전트 배포/실행, 클라이언트 수 할당, 결과 수집/집계. 수만 규모 부하 테스트
