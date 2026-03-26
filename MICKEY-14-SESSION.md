# MICKEY-14-SESSION

## Session Goal
Phase 3 E2E 재검증 + Phase 4 Step 1 + BT 단계 A/B 구현

## Previous Context
Mickey 13: Phase 3 완료 (ScenarioBuilder + DynamicFieldInterceptor, 테스트 119개).

## Current Tasks
- [x] 인터셉터 Priority 체이닝 + 배열 표시 개선
- [x] user_input_fields + {random:N} 지원
- [x] mmorpg_simulator charUid → 서버 AUTO_INCREMENT
- [x] 카탈로그 재생성 + SessionState flat key 수정 + 랜덤 계정 E2E
- [x] Phase 4 Step 1: LoadTestRunner (--clients 5 E2E 성공)
- [x] ReplayLogger: JSON Lines 파일 로그 + 날짜별 롤링
- [x] BT 단계 A: 노드 모델 + JSON 직렬화 + ConditionEvaluator
- [x] BT 단계 B: ActionExecutor + BehaviorTreeExecutor + CLI (--behavior)
- [x] BT E2E: 신규 계정(count==0→create+select) + 기존 계정(count>0→select) 양쪽 분기 검증
- [ ] BT 단계 C: RecordingStore + 전환 조건 자동 추출

## Key Decisions
- 인터셉터 Priority (0=필드주입, 100=게임로직)
- Phase 4 로드맵: Step 1(동기) → Step 2(async) → Step 3(멀티에이전트)
- BT 설계: docs/BEHAVIOR_TREE_DESIGN.md 참조
- ActionExecutor: 기존 PacketBuilder/Handler/Interceptor 재사용, PacketReplayer 수정 없음

## Files Modified
- BehaviorTree.cs, ConditionEvaluator.cs, ActionExecutor.cs, BehaviorTreeExecutor.cs (신규)
- LoadTestRunner.cs, ReplayLogger.cs (신규)
- IReplayInterceptor.cs, NpcAttackInterceptor.cs, PacketReplayer.cs, Program.cs
- ActionCatalogBuilder.cs, ScenarioBuilder.cs
- protocols/mmorpg_simulator.json, actions/mmorpg_simulator_actions.json
- PURPOSE-SCENARIO.md, docs/BEHAVIOR_TREE_DESIGN.md
- behaviors/auto_gameplay.json, behaviors/existing_account.json
- (mmorpg_simulator) Protocol.h, GameServer/main.cpp, GameClient/main.cpp, add_spCharacterCreate.sql

## Lessons Learned
- SessionState 배열 통째 저장 → flat key lookup 실패. 재귀 펼침 필수
- 랜덤 데이터로 검증해야 진짜 동작 확인 가능
- JsonElement→string 변환 후 ResolveValue 호출 필수 (BT overrides)
- FileShare.ReadWrite 필수 (Windows 다중 클라이언트 파일 로그)

## Context Window Status
18%

## Next Steps
- BT 단계 C: RecordingStore + 전환 조건 자동 추출
- BT 단계 D: BehaviorTreeBuilder (녹화 → 트리 자동 생성)
