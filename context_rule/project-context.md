# Project Context

## Environment
- .NET 9.0 Console App (C#), Windows Raw Socket
- WSL2에서 개발, 빌드는 Windows dotnet 경로 사용: `"/mnt/c/Program Files/dotnet/dotnet.exe"`
- 실행은 Windows (관리자 권한 필요)
- 외부 NuGet 의존성 없음
- SDK 10.0.200 (net9.0 타겟 호환)

## Goal
TCP 패킷 캡처 → JSON 프로토콜 파싱 → 로그 → 재현 → QA 자동화 (부하/회귀 테스트)

## Constraints
- Windows 전용 (Raw Socket), TCP only, 127.0.0.1 loopback 캡처 불가
- 관리자 권한 필요
- 관련 프로젝트: ../mmorpg-simulator/ (검증용 mockup 게임)

## Key Decisions
- 자율성 Level 2 (Balanced) — Mickey 1

## Known Issues
- README의 `--port` CLI 옵션이 실제 미구현 (인터랙티브 입력만)

## Lessons Learned
- WSL2에서 dotnet 빌드 시 Windows 경로 필요
- SC_CHAR_INFO 이중 용도: 내 캐릭터 응답(CS_CHAR_SELECT 직후)과 타인 브로드캐스트가 동일 패킷. charUid 매칭으로 구분 필수
- 노이즈 패킷 중 데이터 소스 역할(SC_NPC_SPAWN→npcUid)이 있으므로, 노이즈 분류 시 "리플레이 제외"와 "데이터 수집 제외"를 구분해야 함
- DB 스키마 변경 시 CREATE TABLE IF NOT EXISTS 사용 전 반드시 DESCRIBE로 기존 스키마 diff 확인. IF NOT EXISTS는 스키마 불일치를 숨겨 런타임 에러 유발
## Common Commands
- 빌드: `cd PacketCaptureAgent && "/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release`
- 캡처 (Windows): `PacketCaptureAgent.exe -p ../protocols/mmorpg_simulator.json`
- 재현 (Windows): `PacketCaptureAgent.exe -p ../protocols/mmorpg_simulator.json -r capture.log -t host:port`

## Last Updated
2026-03-15
