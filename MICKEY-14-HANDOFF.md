# MICKEY-14-HANDOFF

## Current Status
Phase 4 Step 1 완료 + BT 단계 A~E 완료. 다음: mmorpg_simulator 퀘스트/파티 시스템 추가 후 BT E2E 검증.

## Next Steps
1. mmorpg_simulator 퀘스트 시스템 구현 (FEATURE_PLAN.md #5, 0x0Axx)
2. mmorpg_simulator 파티 시스템 구현 (FEATURE_PLAN.md #6, 0x0Bxx)
3. packet-capture-log-agent 프로토콜 JSON 동기화
4. 다양한 시나리오 캡처 → BT 자동 생성 → 편집 → 실행 E2E 검증

## Important Context
- BT 단계 F(LLM 조건 폴백) 미구현 — 필요 시 추가
- ActionExecutor에서 {random:N} 해석: JsonElement→ConvertJsonElement→ResolveValue 순서 필수
- 퀘스트 패킷 번호: 0x0Axx, 파티: 0x0Bxx (Protocol.h 기존 체계 유지)
- mmorpg_simulator 빌드: Visual Studio 2022 (v143 toolset)

## Quick Reference
- SESSION: MICKEY-14-SESSION.md
- BT 설계: docs/BEHAVIOR_TREE_DESIGN.md
- mmorpg_simulator 계획: ../mmorpg_simulator/FEATURE_PLAN.md
- 테스트: `"/mnt/c/Program Files/dotnet/dotnet.exe" test PacketCaptureAgent.Tests --no-restore -v quiet`
- Context window: 18%
