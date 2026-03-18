# MICKEY-5-SESSION

## Session Goal
Mickey 3 핸드오프 TODO 소화 + Phase 3 실현 가능성 분석 + mmorpg_simulator 기능 추가 계획

## Previous Context
Mickey 4: 하우스키핑 (아카이빙 규칙 + Mickey 1-2 아카이빙). T1.5 원본 동기화 미완료→확인 결과 이미 동기화 상태.
Mickey 3: E2E 파이프라인 정상 동작 확인. mmorpg_simulator.json 변경 미커밋.

## Current Tasks
- [x] mmorpg_simulator.json size_field/type_field 커밋+push | CC: b57138b
- [x] 미사용 코드 정리 + 리플레이 구조 리팩토링 | CC: IResponseHandler 기반 분리, 빌드 성공
- [x] SC_CHAR_LIST count_field 연결 | CC: length:5 → count_field:count
- [x] Mickey 문서 + README 히스토리 섹션 추가 후 커밋+push | CC: 77b64e4
- [x] T1.5 원본 동기화 확인 | CC: 이미 동일, 작업 불필요
- [x] Phase 3 실현 가능성 분석 | CC: 프로토타이핑 완료, BT 기반 TestPlay 설계
- [x] mmorpg_simulator 기능 추가 계획 수립 | CC: 채팅→인벤토리→상점→파티 순서

## Progress
### Completed
- mmorpg_simulator.json size_field/type_field 커밋+push (b57138b)
- Replay()+ReplayWithParsing() → IResponseHandler 기반 단일 Replay()로 리팩토링
  - RawResponseHandler, ParsingResponseHandler, ReplayContext 도입
- FormatJson() 제거, ShowUsage() → --help/-h 연결
- SC_CHAR_LIST chars 배열: 고정 length:5 → count_field:count
- 리팩토링 커밋+push (05f625e)
- Mickey 문서 24개 + README 히스토리 섹션 커밋+push (77b64e4)
- Phase 3 프로토타이핑: 패킷 분류(essential/gameplay/noise), 동적 필드 추적, 다른 플레이어 패킷 분석
- TestPlay 설계: BT 기반 아키텍처, GameState, 시나리오 JSON, 다중 클라이언트
- mmorpg_simulator 현재 기능 분석 + 추가 기능 계획 (FEATURE_PLAN.md)

## Key Decisions
- 리플레이 구조: 코어 루프(Replay)와 응답 처리(IResponseHandler)를 분리
- Phase 3: 단순 Replay(회귀)와 TestPlay(BT 기반 부하/시나리오)를 별도 모듈로 분리
- mmorpg_simulator 기능 추가 순서: 채팅 → 인벤토리/아이템 → 상점 → 파티

## Files Modified
- PacketCaptureAgent/PacketReplayer.cs (리팩토링)
- PacketCaptureAgent/PacketFormatter.cs (FormatJson 제거)
- PacketCaptureAgent/Program.cs (--help 연결, ParsingResponseHandler 사용)
- protocols/mmorpg_simulator.json (size_field/type_field, count_field)
- README.md (Mickey 히스토리 섹션)
- prototype/ (분석 스크립트, 결과, 설계 문서 4개)
- mmorpg_simulator/FEATURE_PLAN.md (기능 추가 계획)

## Lessons Learned
- 리플레이 함수 설계 시 "코어 루프"와 "데이터 처리"를 처음부터 분리해야 변형 추가 시 코드 중복 방지
- SC_CHAR_INFO가 이중 용도(내 정보 + 타인 브로드캐스트) — charUid 매칭으로 구분 필요
- [Protocol] T1.5 원본 동기화는 diff로 먼저 확인하면 불필요한 작업 방지 가능

## Context Window Status
정상

## Next Steps
- mmorpg_simulator 채팅 기능 구현 (FEATURE_PLAN.md §1)
- 이후 인벤토리/아이템 → 상점 → 파티 순서
