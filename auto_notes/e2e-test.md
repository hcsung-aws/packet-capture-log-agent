# E2E Test Notes

## 실행 환경
- 서버/클라이언트/캡처 에이전트 모두 Windows에서 실행 (관리자 권한)
- 127.0.0.1 사용 불가 (Raw Socket loopback 제한) → 캡처 에이전트에 표시되는 비-loopback IP 사용

## mockdb SP 적용 순서 (mmorpg_simulator/scripts/)
1. fix_spAccountLogin.sql
2. add_spCharacterCreate.sql
3. add_spCharacterList.sql
4. attendance_system.sql
5. gold_system.sql (character.Gold 컬럼 추가 + spCharacterLogin 교체 + spAttendanceCheck 업데이트)

## 프로토콜 JSON 주의
- size_field/type_field 반드시 명시 (기본값 "size"가 필드명 "length"와 불일치)
- PacketCaptureAgent 실행 시 프로토콜 파일은 절대경로 사용

## 캡처→재현 파이프라인
- 캡처: `PacketCaptureAgent.exe -p <절대경로>/mmorpg_simulator.json` → 인터페이스 선택 → 포트 9000
- 재현: `PacketCaptureAgent.exe -p <절대경로>/mmorpg_simulator.json -r <로그파일> -t <IP>:9000`
- 재현 모드: timing / response / hybrid(기본)
