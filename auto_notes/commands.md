# Commands

## Build
- `cd PacketCaptureAgent && dotnet build -c Release`
- `cd PacketCaptureAgent && dotnet build` (Debug)

## Run (Windows, 관리자 권한)
- 캡처: `PacketCaptureAgent.exe -p ../protocols/mmorpg_simulator.json`
  - 인터페이스 선택 → 포트 입력 → 캡처 시작
- 재현: `PacketCaptureAgent.exe -p ../protocols/mmorpg_simulator.json -r capture.log -t host:port`
  - 옵션: `--mode timing|response|hybrid`, `--speed 1.0`, `--timeout 5000`

## Notes
- Raw Socket 사용 → Windows 관리자 권한 필수
- WSL2에서는 빌드만 가능, 실행은 Windows에서
- 127.0.0.1 loopback 캡처 불가 (Windows 제한)
