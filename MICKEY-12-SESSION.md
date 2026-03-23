# MICKEY-12-SESSION

## Session Goal
교훈 승격 리뷰 + 세션 로그 아카이빙 → 이후 작업 TBD

## Previous Context
Mickey 11: Mermaid 시퀀스 다이어그램 + Phase별 rect 구분 완료, SC_INVENTORY_LIST 변경, 테스트 91개 통과.

## Current Tasks
- [x] 교훈 승격 리뷰 (Mickey 9~10) | CC: 승격 항목 사용자 확인 완료
- [x] 세션 로그 아카이빙 (Mickey 9, 10 → sessions/) | CC: 루트에 Mickey 11만 남음
- [x] Task 1: ParseLog 배열 필드 확장 | CC: chars[0].charUid 등 flat key 추출, 기존 테스트 통과
- [x] Task 2: Dynamic 필드 자동 식별 | CC: DetectDynamicFields()가 charUid/targetUid 의존성 감지
- [x] Task 2.5: Action Catalog 생성 | CC: --analyze 시 actions/{protocol}_actions.json 생성, merge 전략 동작
- [x] Q 키 캡처 종료 + 자동 분석 | CC: Q→분석→카탈로그 업데이트, Ctrl+C→즉시 종료
- [x] Suffix 타입 필터 + 수동 매핑 오버라이드 | CC: uid↔uid만 매칭, field_mappings 우선
- [x] 시간 순서 버그 수정 | CC: SEND 이전 RECV만 후보, 4건 오탐 해결
- [x] Git push (양쪽 레포) | CC: packet-capture-log-agent + mmorpg_simulator push 완료

## Progress
- [x] 교훈 승격: sequence-analysis.md (context_rule/), shared-constants.md (common_knowledge/)
- [x] INDEX 갱신: context_rule/INDEX.md, common_knowledge/INDEX.md
- [x] auto_notes/observations.md 해결 완료 항목 정리 (3건 제거)
- [x] 아카이빙: Mickey 9, 10 → sessions/
- [x] Task 1: PacketFormatter 배열 flat key 출력 + ParseLog regex 확장 (테스트 93개)
- [x] Task 2: DetectDynamicFields() 구현 (RECV→SEND 값 매칭, 중복 제거) (테스트 95개)
- [x] Task 2.5: ActionCatalogBuilder 구현 (build + merge + stale 제거 + Program.cs 통합) (테스트 102개)

## Key Decisions
- 프로토콜 이름 패턴 폴백 → context_rule/ 승격 (Phase 3 작업에서 반복 참조)
- 공유 상수 패턴 → common_knowledge/ 승격 (범용 패턴)
- Action Catalog: merge 전략 (추가/갱신/유지/stale 제거) + per-action source 추적
- Action ID: SEND 패킷명에서 CS_ 제거 + lowercase (CS_LOGIN → login)
- Dynamic Field: suffix 타입 필터 (uid↔uid, id↔id, slot↔slot) + 시간 순서 준수
- 수동 매핑: 프로토콜 JSON field_mappings (external/static 지원)
- Q 키 캡처 종료 → 자동 분석 (Ctrl+C는 기존 동작 유지)

## Files Modified
- context_rule/sequence-analysis.md (신규)
- common_knowledge/shared-constants.md (신규)
- context_rule/INDEX.md, common_knowledge/INDEX.md (갱신)
- auto_notes/observations.md (정리)
- PacketCaptureAgent/PacketFormatter.cs (배열/구조체 flat key 출력)
- PacketCaptureAgent/PacketReplayer.cs (ParseLog regex 확장)
- PacketCaptureAgent/SequenceAnalyzer.cs (DynamicField + DetectDynamicFields + suffix 타입 필터)
- PacketCaptureAgent/ActionCatalogBuilder.cs (신규 — 모델 + 빌더 + merge + I/O)
- PacketCaptureAgent/Protocol.cs (FieldMapping 모델 추가)
- PacketCaptureAgent/Program.cs (RunAnalyzeMode 카탈로그 통합 + Q 키 종료)
- PacketCaptureAgent.Tests/PacketFormatterTests.cs (배열 테스트 2건)
- PacketCaptureAgent.Tests/SequenceAnalyzerTests.cs (DynamicField + suffix + 수동매핑 테스트 7건)
- PacketCaptureAgent.Tests/ActionCatalogBuilderTests.cs (신규 — 7건)

## Lessons Learned
- [Protocol] 값 기반 매칭만으로는 게임 엔티티 ID 겹침 오탐 발생 → suffix 타입 필터 필수
- 동적 필드 감지 시 시간 순서(SEND 이전 RECV만 후보) 미준수하면 결과 패킷이 소스로 잡히는 근본적 오류 발생
- logWriter가 using 블록 내에서 열려 있으면 같은 파일을 읽을 수 없음 → Dispose 후 분석 실행

## Context Window Status
40%

## Next Steps
- Phase 3 계속: 시나리오 자동 생성 (Action Catalog에서 Action 조립 → 재현 가능 시나리오 파일)
