# Project Context

## Environment
- .NET 9.0 Console App (C#), Windows Raw Socket
- WSL2에서 개발, 빌드는 Windows dotnet 경로 사용: `"/mnt/c/Program Files/dotnet/dotnet.exe"`
- 실행은 Windows (관리자 권한 필요)
- 외부 NuGet 의존성 없음
- SDK 10.0.200 (net9.0 타겟 호환)
- AgentCore: Python 3.12+ (boto3, flask), AWS (Bedrock, Lambda, Step Functions, S3, API Gateway)
- Terraform 1.5+ (인프라 배포)

## Goal
TCP 패킷 캡처 → JSON 프로토콜 파싱 → 로그 → 재현 → BT 자동 생성 → QA 자동화 + 프로토콜 자동 생성

## Constraints
- Windows 전용 (Raw Socket), TCP only, 127.0.0.1 loopback 캡처 불가
- 관리자 권한 필요
- 관련 프로젝트: ../mmorpg_simulator/ (검증용 mockup 게임)
- AgentCore: Bedrock 모델 접근 권한 필요 (inference profile ID 사용)

## Key Decisions
- 자율성 Level 2 (Balanced) — Mickey 1
- AgentCore는 AWS 클라우드, 로컬은 HTTP 클라이언트만 → NuGet 의존성 불필요
- 멀티 에이전트 상호 검증: Generator + Reviewer 2-agent 패턴
- 단계별 독립 실행 + 파이프라인 래핑 (디버깅/유지보수 원칙)
- Lambda: Python, 인프라: Terraform

## Known Issues
- PacketCaptureAgent는 bin/Debug/net9.0에서 실행됨 → 프로토콜 파일 상대경로 주의 (절대경로 권장)
- Lambda Layer는 python/ prefix 필수 (layer_pkg/python/ 구조)
- Bedrock on-demand는 inference profile ID 필요 (모델 ID 직접 사용 불가)
- PacketBuilder.GetList()는 List<object>만 인식. List<Dictionary<string,object>>는 빈 리스트 처리 → (object) 캐스트 필수

## Lessons Learned
- WSL2에서 dotnet 빌드 시 Windows 경로 필요
- SC_CHAR_INFO 이중 용도: charUid 매칭으로 구분 필수
- 노이즈 분류 시 "리플레이 제외"와 "데이터 수집 제외" 구분 필요
- LLM JSON 응답에 markdown fence 포함 가능 → raw_decode로 첫 JSON 추출
- Step Functions에서 Lambda 반환값으로 다음 단계 입력 구성 시, 필요 데이터를 Lambda가 직접 반환해야 함

## Common Commands
- 빌드: `cd PacketCaptureAgent && "/mnt/c/Program Files/dotnet/dotnet.exe" build -c Release`
- 테스트: `"/mnt/c/Program Files/dotnet/dotnet.exe" test PacketCaptureAgent.Tests --no-restore -v quiet`
- 캡처 (Windows): `PacketCaptureAgent.exe -p ../protocols/mmorpg_simulator.json`
- 재현 (Windows): `PacketCaptureAgent.exe -p protocol.json -r capture.log -t host:port`
- 분석: `PacketCaptureAgent.exe -p protocol.json --analyze capture.log`
- BT 생성: `PacketCaptureAgent.exe -p protocol.json --build-behavior`
- BT 실행: `PacketCaptureAgent.exe -p protocol.json --behavior bt.json -t host:port --duration 60`
- 일괄 분석: `.\analyze_all.ps1 ..\protocols\mmorpg_simulator.json ..\captures\archive`
- AgentCore 배포: `cd agent-core/terraform && terraform apply`
- 프로토콜 생성 CLI: `cd agent-core/client && python3 cli.py generate --source /path/to/source`
- 프로토콜 생성 웹: `cd agent-core/client && python3 app.py 8090`

## Last Updated
2026-04-01
