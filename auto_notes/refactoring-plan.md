# 대규모 리팩토링 계획

> Mickey 19에서 코드 리뷰 후 식별. 변경 범위가 크므로 별도 세션에서 단계별 진행.

## 1. Program.cs 모드별 클래스 분리

현재 849줄, 8개 RunXxxMode + BtSyncHandler + ShowUsage가 한 파일에 집중.

### 단계
1. **모드 인터페이스 정의**: `IMode { void Run(CliOptions cli); }`
2. **모드별 클래스 추출** (우선순위 순):
   - `CaptureMode.cs` ← RunCaptureMode (가장 독립적)
   - `AnalyzeMode.cs` ← RunAnalyzeMode
   - `ReplayMode.cs` ← RunReplayMode
   - `ScenarioMode.cs` ← RunBuildScenarioMode + RunScenarioReplayMode
   - `BehaviorTreeMode.cs` ← RunBuildBehaviorMode + RunBehaviorTreeMode + RunEditBehavior + RunWebEditor
   - `FsmMode.cs` ← RunBuildFsmMode + RunFsmMode
3. **BtSyncHandler 분리**: 별도 파일 또는 BehaviorTreeMode 내부 클래스
4. **Program.cs**: Main + ParseArgs + ShowUsage + 모드 디스패치만 남김
5. **각 단계마다 테스트 실행** (characterization tests가 안전망)

### 예상 결과
- Program.cs: ~150줄 (현재 849줄)
- 모드별 파일: 각 50~150줄

## 2. ScenarioBuilder.cs에서 Interceptor/Handler 분리

현재 ScenarioBuilder.cs에 3개 관심사가 혼재:
- ScenarioBuilder (시나리오 조립)
- DynamicFieldInterceptor (IReplayInterceptor 구현)
- TrackingResponseHandler (IResponseHandler 구현)

### 단계
1. `DynamicFieldInterceptor.cs` 추출 (IReplayInterceptor 구현)
2. `TrackingResponseHandler.cs` 추출 (IResponseHandler 구현)
3. ScenarioBuilder.cs에는 순수 시나리오 로직만 남김

### 예상 결과
- ScenarioBuilder.cs: ~200줄 (현재 306줄)
- DynamicFieldInterceptor.cs: ~30줄
- TrackingResponseHandler.cs: ~25줄

## 전제조건
- 144개 테스트 통과 상태 유지
- 각 추출 단계마다 빌드 + 테스트 검증
- 기능 변경 없음 (순수 구조 리팩토링)

## Last Updated
2026-04-05, Mickey 19
