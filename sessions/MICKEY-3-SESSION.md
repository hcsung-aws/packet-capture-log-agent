# MICKEY-3-SESSION

## Session Goal
dbtestclient git 정리 + E2E 검증 (캡처→재현 파이프라인 실제 동작 확인)

## Previous Context
Mickey 2: MySQL/MSSQL mockdb 스키마 설치, E2E 테스트 준비 중

## Current Tasks
- [x] dbtestclient 미커밋 변경사항 분류 + 커밋 + push | CC: git status clean, 5개 의미 단위 커밋
- [x] E2E 테스트 가이드 정리 | CC: 캡처/재현 단계별 가이드 제공
- [x] mockdb SP 불일치 해결 | CC: GameServer 6개 SP 모두 정상 호출
- [x] 프로토콜 JSON 버그 수정 | CC: 캡처 시 파싱된 패킷 출력 확인
- [x] E2E 캡처→재현 검증 | CC: 캡처 로그 생성 + 재현 모드 동작 확인

## Progress
### Completed
- dbtestclient: .gitattributes(CRLF) + .gitignore 패턴화 + StreamWriter→TextWriter + MockupCppServer + ODBCTestClient failover + MockupServerless/SQL 스크립트 (5커밋, 394b0b4..5d604b1)
- mockdb SP 수정: mmorpg_simulator/scripts/ 전체 적용 (fix_spAccountLogin, add_spCharacterCreate, add_spCharacterList 신규 생성, attendance_system, gold_system)
- 프로토콜 JSON 버그 수정: size_field/type_field 누락 → 추가 (mmorpg_simulator.json)
- E2E 캡처→재현 파이프라인 정상 동작 확인

## Key Decisions
- dbtestclient .gitignore를 패턴 기반으로 전면 교체 (개별 DLL 나열 → 글로벌 패턴)
- spCharacterList SP 신규 생성 (기존 스크립트에 없었음)

## Files Modified
- protocols/mmorpg_simulator.json (size_field/type_field 추가)
- mmorpg_simulator/scripts/add_spCharacterList.sql (신규)

## Lessons Learned
- [Protocol] mmorpg_simulator/scripts/에 GameServer용 SP 마이그레이션 스크립트가 모두 있음. 적용 순서: fix_spAccountLogin → add_spCharacterCreate → add_spCharacterList → attendance_system → gold_system
- 프로토콜 JSON의 size_field/type_field 누락 시 기본값("size")이 필드명("length")과 불일치 → int32 폴백 → 패킷 크기 오판으로 전체 파싱 실패
- PacketCaptureAgent는 bin/Debug/net9.0에서 실행되므로 프로토콜 파일은 절대경로 필요
- WSL2 환경 CRLF 문제: .gitattributes(eol=crlf) + git add --renormalize로 해결

## Context Window Status
정상

## Next Steps
- protocols/mmorpg_simulator.json 변경 커밋
- 코드 정리 (미사용 메서드, 배열 count_field 이슈)
- 시나리오 자동 조립 설계 (Phase 3)
