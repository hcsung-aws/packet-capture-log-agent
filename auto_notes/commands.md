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

## mmorpg_simulator
- 빌드: Visual Studio 2022 (v143 toolset), VS 2026에서는 v143 개별 구성 요소 설치 필요
- 실행: 프로젝트 루트에서 `run_server.bat`, `run_client.bat` (data/ 경로 참조)
- 배포: `deploy.bat` → deploy/ 디렉토리에 exe+data+scripts 패키징
- DB 스크립트: `scripts/inventory_system.sql` (mysql -h 127.0.0.1 -u admin -p mockdb < scripts/inventory_system.sql)
- MySQL 접속 (WSL): `"/mnt/c/Program Files/MySQL/MySQL Server 8.0/bin/mysql.exe" -h 127.0.0.1 -u admin -p'flfhelem1!' mockdb`
- 빌드 (Windows): 프로젝트 루트에서 `build.bat`
