# FILE-STRUCTURE

## Directory Tree
```
packet-capture-log-agent/
├── PacketCaptureAgent/            # 메인 소스 (C# .NET 9.0)
│   ├── Program.cs                 # 진입점 (캡처/재현/분석/BT/FSM/에이전트/매니저 모드 분기)
│   ├── Protocol.cs                # JSON 프로토콜 정의 모델 (semantics 포함)
│   ├── PacketParser.cs            # TCP 스트림 → 동적 패킷 파싱 (string_prefixed, conditional)
│   ├── PacketBuilder.cs           # 파싱 데이터 → 바이너리 패킷 재구성
│   ├── PacketReplayer.cs          # 로그 파싱 + TCP 재전송
│   ├── PacketFormatter.cs         # 콘솔/파일 출력 포맷
│   ├── RawPacketParser.cs         # 원시 패킷 파싱
│   ├── TcpStream.cs               # TCP 스트림 재조립 + 연결 관리
│   ├── CaptureSession.cs          # 캡처 세션 관리
│   ├── GameWorldState.cs          # 리플레이 중 게임 상태 추적
│   ├── ReplayLogger.cs            # 리플레이 로깅
│   ├── SequenceAnalyzer.cs        # 요청-응답 시퀀스 매칭 (이름 패턴 + 위치 폴백)
│   ├── ScenarioBuilder.cs         # 녹화 → 시나리오 빌드 + ConvertJsonElement 유틸
│   ├── RecordingStore.cs          # 녹화 저장소
│   ├── FieldFlattener.cs          # 중첩 필드 평탄화 유틸리티
│   ├── BehaviorTree.cs            # BT 노드 모델 + JSON 직렬화
│   ├── BehaviorTreeBuilder.cs     # 녹화 → BT 자동 생성 (조건 정제 + 상태 바인딩 + weight)
│   ├── BehaviorTreeExecutor.cs    # BT 런타임 실행 (duration 루프)
│   ├── BehaviorTreeEditor.cs      # BT CLI 편집기
│   ├── BehaviorTreeWebEditor.cs   # BT 웹 에디터 (HttpListener + 바닐라 JS)
│   ├── ConditionEvaluator.cs      # BT 조건 평가
│   ├── ActionExecutor.cs          # BT 리프 노드 실행 (field_variants 랜덤 선택)
│   ├── ActionCatalogBuilder.cs    # 캡처 로그 → 액션 카탈로그 (field_variants 수집)
│   ├── FsmDefinition.cs           # FSM 전이 확률 모델
│   ├── FsmBuilder.cs              # 녹화 → FSM 전이 확률 생성
│   ├── FsmExecutor.cs             # FSM 런타임 실행
│   ├── LoadTestRunner.cs          # 부하 테스트 실행 (멀티 클라이언트)
│   ├── ManagerRunner.cs           # 멀티 에이전트 매니저 (에이전트 분배 + 결과 집계)
│   ├── AgentServer.cs             # 에이전트 HTTP API 서버 (원격 제어 수신)
│   ├── IReplayInterceptor.cs      # 인터셉터 인터페이스 + ReplayContext
│   ├── NpcAttackInterceptor.cs    # NPC 공격 동적 대상 교체
│   ├── ProximityInterceptor.cs    # 근접 기반 인터셉터
│   ├── IPacketTransform.cs        # 패킷 변환 인터페이스 + 팩토리
│   ├── RsaDecryptor.cs            # RSA 복호화
│   ├── XteaDecryptor.cs           # XTEA 복호화
│   ├── wwwroot/                   # 웹 에디터 정적 파일
│   ├── analyze_all.ps1            # 다중 로그 일괄 분석 (PowerShell)
│   └── analyze_all.bat            # 다중 로그 일괄 분석 (Batch)
├── PacketCaptureAgent.Tests/      # 단위 테스트 (14파일, 162개)
├── agent-core/                    # 프로토콜 자동 생성 (LLM Agent)
│   ├── poc/                       # 로컬 PoC (Bedrock 직접 호출, Lambda 대체됨)
│   ├── lambda/                    # AWS Lambda 함수 (5 Phase + Orchestrator + Authorizer)
│   ├── terraform/                 # 인프라 정의 (S3, Lambda, Step Functions, API GW)
│   └── client/                    # CLI + 웹 프론트엔드
├── protocols/
│   └── mmorpg_simulator.json      # MMORPG 시뮬레이터 (51 packets)
├── recordings/                    # 녹화 데이터
├── actions/                       # 액션 카탈로그
├── behaviors/                     # BT/FSM JSON
├── captures/                      # 캡처 로그 아카이브
├── scenarios/                     # (비어 있음, ScenarioBuilder 코드는 유지)
├── logs/                          # 실행 로그
├── sessions/                      # 아카이빙된 세션 파일 (MICKEY-1~23)
├── docs/
│   ├── BEHAVIOR_TREE_DESIGN.md    # BT 아키텍처 설계
│   ├── PROTOCOL_SCHEMA.md         # 프로토콜 JSON 스키마 가이드
│   ├── E2E_TEST_GUIDE.md          # E2E 테스트 가이드
│   └── DEPLOYMENT_GUIDE.md        # AgentCore 배포 가이드
├── context_rule/                  # 프로젝트 특화 규칙 (5개)
├── common_knowledge/              # 범용 재사용 패턴 (9개)
└── auto_notes/                    # 자동 관찰 메모 (9개)
```

## Key Files
- Config: PacketCaptureAgent.csproj (.NET 9.0, no NuGet dependencies)
- Entry: Program.cs
- Infra: agent-core/terraform/main.tf
- Docs: docs/PROTOCOL_SCHEMA.md, docs/BEHAVIOR_TREE_DESIGN.md, docs/E2E_TEST_GUIDE.md, docs/DEPLOYMENT_GUIDE.md, README.md

## File Statistics
- C# source: 36 files / 5,850 lines
- C# tests: 14 files / 2,863 lines / 162 tests
- Python (AgentCore): ~20 files (PoC 8 + Lambda 10 + Client 2)
- Terraform: 3 files / 446 lines
- Protocol definitions: 1 active (mmorpg_simulator.json, 51 packets)

## Project Structure Pattern
C# 콘솔 앱 (패킷 캡처/재현/BT/FSM/멀티에이전트) + Python AgentCore (프로토콜 자동 생성, AWS 배포)

## Last Updated
2026-04-09
