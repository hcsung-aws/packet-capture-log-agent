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
- SC_CHAR_LIST 배열이 count_field 미연결 (고정 length:5)
- `ShowUsage()`, `Replay()`, `FormatJson()` 미사용 코드 존재
- 빌드 경고 CA1416 (Windows 전용 API) — 의도된 동작

## Lessons Learned
- WSL2에서 dotnet 빌드 시 Windows 경로 필요

## Common Commands
- 빌드: `cd PacketCaptureAgent && "/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release`
- 캡처 (Windows): `PacketCaptureAgent.exe -p ../protocols/mmorpg_simulator.json`
- 재현 (Windows): `PacketCaptureAgent.exe -p ../protocols/mmorpg_simulator.json -r capture.log -t host:port`

## Last Updated
2026-03-11
