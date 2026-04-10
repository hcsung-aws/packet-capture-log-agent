# 대규모 리팩토링 계획 — ✅ 전체 완료

> Mickey 19에서 식별, **Mickey 24에서 전체 완료**. 이 문서는 아카이브 상태.

## 1. Program.cs 모드별 클래스 분리 ✅

- 861줄 → 196줄
- 8개 static 클래스: CaptureMode, AnalyzeMode, ReplayModeRunner, ScenarioMode, BehaviorTreeMode, FsmMode, AgentMode, ManagerMode
- BtSyncHandler 별도 파일 (BT/FSM 공용)
- IMode 인터페이스 대신 static 클래스 채택 (CLI 도구에서 과도한 추상화 불필요)

## 2. ScenarioBuilder.cs에서 Interceptor/Handler 분리 ✅

- DynamicFieldInterceptor.cs 추출
- TrackingResponseHandler.cs 추출
- ScenarioBuilder.cs에는 순수 시나리오 로직만 잔존

## Last Updated
2026-04-09, Mickey 24
