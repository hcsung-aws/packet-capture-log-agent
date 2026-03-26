# MICKEY-14-SESSION

## Session Goal
Phase 3 E2E 재검증 + Phase 4 Step 1 구현 (다중 클라이언트 동시 재현)

## Previous Context
Mickey 13: Phase 3 완료 (ScenarioBuilder + DynamicFieldInterceptor, 테스트 119개).

## Current Tasks
- [x] 시나리오 조립 + 재현 E2E 검증
- [x] 인터셉터 Priority 체이닝 + 배열 표시 개선
- [x] user_input_fields + {random:N} 지원
- [x] mmorpg_simulator charUid → 서버 AUTO_INCREMENT
- [x] 카탈로그 재생성 + SessionState flat key 수정 + 랜덤 계정 E2E
- [x] Phase 4 Step 1: LoadTestRunner (--clients N)
- [x] 인터셉터 출력 억제 (ReplaySession.Output)
- [x] ReplayLogger: JSON Lines 파일 로그 + 날짜별 롤링

## Progress
- Phase 3 완전 검증 + Phase 4 Step 1 완료
- 5클라이언트 동시 재현 E2E 성공 (5/5)
- JSON Lines 파일 로그 정상 동작

## Key Decisions
- 인터셉터 Priority (0=필드주입, 100=게임로직), 등록 순서 무관
- CS_CHAR_CREATE charUid: 클라이언트→서버 AUTO_INCREMENT
- user_input_fields: SEND string 필드 중 dynamic_fields 아닌 것 자동 감지
- Phase 4 로드맵: Step 1(동기) → Step 2(async) → Step 3(멀티에이전트)
- 파일 로그: JSON Lines + UnsafeRelaxedJsonEscaping, FileShare.ReadWrite

## Files Modified
- IReplayInterceptor.cs, NpcAttackInterceptor.cs, ScenarioBuilder.cs, PacketReplayer.cs
- Program.cs, ActionCatalogBuilder.cs, LoadTestRunner.cs, ReplayLogger.cs
- protocols/mmorpg_simulator.json, actions/mmorpg_simulator_actions.json
- PURPOSE-SCENARIO.md
- (mmorpg_simulator) Protocol.h, GameServer/main.cpp, GameClient/main.cpp, add_spCharacterCreate.sql

## Lessons Learned
- SessionState 배열 통째 저장 → flat key lookup 실패. 재귀 펼침 필수
- 동적 필드 "우연히" 성공 주의 — 랜덤 데이터로 검증 필수
- 부하테스트 시 모든 출력 경로를 TextWriter로 통일
- 다중 클라이언트 파일 로그: FileShare.ReadWrite 필수 (Windows 파일 잠금)

## Context Window Status
18%

## Next Steps
- Phase 5: Behavior Tree 구현 (docs/BEHAVIOR_TREE_DESIGN.md 참조)
  - 단계 A: BT 노드 모델 + JSON 직렬화 + ConditionEvaluator
  - 단계 B: ActionExecutor + BehaviorTreeExecutor + CLI
