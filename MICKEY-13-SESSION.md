# MICKEY-13-SESSION

## Session Goal
Phase 3: 시나리오 자동 조립 — Action Catalog에서 동작 단위를 선택하여 재현 가능한 시나리오 파일 생성

## Previous Context
Mickey 12: Action Catalog 완성 (배열 flat key 파싱, Dynamic Field 자동 감지, suffix 타입 필터, 시간 순서 준수, 수동 오버라이드, 의미 단위 분할 + merge 저장). 테스트 107개 통과.

## Current Tasks
- [x] Mickey 11 아카이빙 + auto_notes 갱신 | CC: 6개 파일 갱신, Mickey 11 → sessions/
- [x] Phase 2→3 전환 검토 | CC: ActionPacket에 SEND 필드 템플릿 누락 확인 → 보강
- [x] Step 0: ActionPacket.Fields 추가 | CC: SEND 패킷 캡처 필드 저장, 107개 통과
- [x] Step 1-4: ScenarioBuilder + DynamicFieldInterceptor + CLI | CC: 119개 통과

## Progress
- [x] ActionPacket.Fields (Dictionary<string, object>?) 추가
- [x] ScenarioBuilder.cs 신규: 모델 + Build + Validate + CollectDynamicFields + BuildInteractive + I/O
- [x] DynamicFieldInterceptor: IReplayInterceptor 구현, 공유 상태 기반 동적 필드 주입
- [x] TrackingResponseHandler: IResponseHandler 래핑, SessionState → 공유 상태 복사
- [x] Program.cs: --build-scenario, -s/--scenario CLI 라우팅 (기존 코드 수정 없음)
- [x] 테스트 12건 신규 (Validate 3, Build 5, CollectDynamicFields 1, Interceptor 3)

## Key Decisions
- 시나리오 기능은 기존 코드와 완전 분리 (새 파일만, 기존 수정 없음)
- 시나리오 파일 = JSON (액션 참조 + repeat + overrides), 캡처 로그와 동일 동작
- 동적 필드 주입: 공유 Dictionary + TrackingResponseHandler + DynamicFieldInterceptor 패턴
- RECV 패킷은 placeholder로 포함 (Replayer가 응답 대기 판단에 사용)

## Files Modified
- PacketCaptureAgent/ActionCatalogBuilder.cs (ActionPacket.Fields 추가)
- PacketCaptureAgent/ScenarioBuilder.cs (신규)
- PacketCaptureAgent/Program.cs (CLI 라우팅 추가)
- PacketCaptureAgent.Tests/ScenarioBuilderTests.cs (신규)

## Lessons Learned
(없음)

## Context Window Status
30%

## Next Steps
- 실제 mmorpg_simulator 대상 E2E 검증 (시나리오 생성 → 재현)
- Action Catalog 재생성 (Fields 포함 버전)
- Phase 4 준비: 다중 클라이언트 동시 재현
