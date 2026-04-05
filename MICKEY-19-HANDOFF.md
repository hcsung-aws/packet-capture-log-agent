# MICKEY-19-HANDOFF

## Current Status
A 단계(품질 개선) 완료. 버그 1건 수정, 중복 7건 해소, BT 테스트 18개 추가. 테스트 119→162개. 대규모 리팩토링 계획 저장.

## Next Steps
B-1(Discovery 파일 선별 개선)부터 진행. B-2(API GW 인증), B-3(모델 비교) 순서.

## Important Context
- ConvertJson(BehaviorTreeBuilder) 제거 → ScenarioBuilder.ConvertJsonElement로 통합. 기존 ConvertJson은 ternary 타입 통합으로 int도 long 반환하는 버그가 있었음
- Program.cs 공통 헬퍼 5개 추출 (LoadProtocol, ParseTarget, CatalogPath, RecordingsPath, LogDir) — 대규모 분리는 auto_notes/refactoring-plan.md 참조
- FieldFlattener.cs 신규 파일 — RecordingStore.Flatten + ParsingResponseHandler.FlattenToState 통합

## Quick Reference
- SESSION: MICKEY-19-SESSION.md
- 테스트: `"/mnt/c/Program Files/dotnet/dotnet.exe" test PacketCaptureAgent.Tests --no-restore -v quiet` (162개)
- 리팩토링 계획: auto_notes/refactoring-plan.md
- Context window: ~50%
