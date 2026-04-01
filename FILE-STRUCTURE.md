# FILE-STRUCTURE

## Directory Tree
```
packet-capture-log-agent/
├── PacketCaptureAgent/            # 메인 소스 (C# .NET 9.0)
│   ├── Program.cs                 # 진입점 (캡처/재현/분석/BT 모드 분기)
│   ├── Protocol.cs                # JSON 프로토콜 정의 모델 (semantics 포함)
│   ├── PacketParser.cs            # TCP 스트림 → 동적 패킷 파싱 (string_prefixed, conditional 포함)
│   ├── PacketBuilder.cs           # 파싱 데이터 → 바이너리 패킷 재구성
│   ├── PacketReplayer.cs          # 로그 파싱 + TCP 재전송
│   ├── PacketFormatter.cs         # 콘솔/파일 출력 포맷
│   ├── TcpStream.cs               # TCP 스트림 재조립 + 연결 관리
│   ├── GameWorldState.cs          # 리플레이 중 게임 상태 추적
│   ├── BehaviorTree.cs            # BT 노드 모델 + JSON 직렬화
│   ├── BehaviorTreeBuilder.cs     # 녹화 → BT 자동 생성 (조건 정제 + 상태 바인딩 + weight)
│   ├── BehaviorTreeExecutor.cs    # BT 런타임 실행 (duration 루프)
│   ├── BehaviorTreeEditor.cs      # BT CLI 편집기
│   ├── BehaviorTreeWebEditor.cs   # BT 웹 에디터 (HttpListener + 바닐라 JS)
│   ├── ActionExecutor.cs          # BT 리프 노드 실행 (field_variants 랜덤 선택)
│   ├── ActionCatalogBuilder.cs    # 캡처 로그 → 액션 카탈로그 (field_variants 수집)
│   ├── IReplayInterceptor.cs      # 인터셉터 인터페이스 + ReplayContext
│   ├── NpcAttackInterceptor.cs    # NPC 공격 동적 대상 교체
│   ├── IPacketTransform.cs        # 패킷 변환 인터페이스 + 팩토리
│   └── analyze_all.ps1            # 다중 로그 일괄 분석 스크립트
├── PacketCaptureAgent.Tests/      # 단위 테스트 (119개)
├── agent-core/                    # 프로토콜 자동 생성 (LLM Agent)
│   ├── poc/                       # 로컬 PoC (Bedrock 직접 호출)
│   │   ├── discovery.py           # Phase 1: 패킷 관련 파일 식별
│   │   ├── analysis.py            # Phase 2: 파일별 메타데이터 추출
│   │   ├── merge.py               # Phase 3: 통합 메타데이터
│   │   ├── generation.py          # Phase 4: 프로토콜 JSON 생성
│   │   ├── validation.py          # Phase 5: 스키마 검증 + 자동 수정
│   │   ├── pipeline.py            # 전체 파이프라인 래핑
│   │   ├── llm_client.py          # Bedrock 호출 (Generator+Reviewer 2-agent)
│   │   └── prompts/               # Phase별 프롬프트 템플릿 (8개)
│   ├── lambda/                    # AWS Lambda 함수
│   │   ├── common/                # 공통 모듈 (llm_client, s3_helper, prompts)
│   │   ├── layer_pkg/             # Lambda Layer 패키지 (python/ prefix)
│   │   ├── discovery/             # Phase 1 Lambda
│   │   ├── analysis/              # Phase 2 Lambda (Map state 병렬)
│   │   ├── merge/                 # Phase 3 Lambda
│   │   ├── generation/            # Phase 4 Lambda
│   │   ├── validation/            # Phase 5 Lambda
│   │   └── orchestrator/          # API Gateway → Step Functions
│   ├── terraform/                 # 인프라 정의
│   │   ├── main.tf                # S3, IAM, Lambda, Step Functions, API Gateway
│   │   ├── variables.tf           # 설정 변수 (리전, 모델 ID)
│   │   └── outputs.tf             # API endpoint, S3 bucket, SFN ARN
│   └── client/                    # 클라이언트
│       ├── cli.py                 # CLI (generate, status, download)
│       ├── app.py                 # 웹 UI (Flask)
│       └── web/index.html         # 프론트엔드 (바닐라 HTML/JS)
├── protocols/                     # 프로토콜 정의 JSON
│   └── mmorpg_simulator.json      # MMORPG 시뮬레이터 (51 packets)
├── recordings/                    # 녹화 데이터
├── actions/                       # 액션 카탈로그
├── behaviors/                     # BT JSON
├── docs/
│   ├── BEHAVIOR_TREE_DESIGN.md    # BT 아키텍처 설계
│   └── PROTOCOL_SCHEMA.md         # 프로토콜 JSON 스키마 가이드
├── context_rule/                  # 프로젝트 특화 규칙
├── common_knowledge/              # 범용 재사용 패턴
└── auto_notes/                    # 자동 관찰 메모
```

## Key Files
- Config: PacketCaptureAgent.csproj (.NET 9.0, no NuGet dependencies)
- Entry: Program.cs
- Infra: agent-core/terraform/main.tf
- Docs: docs/PROTOCOL_SCHEMA.md, docs/BEHAVIOR_TREE_DESIGN.md, README.md

## File Statistics
- C# source: ~20 files
- Python (AgentCore): ~15 files
- Terraform: 3 files
- Protocol definitions: 1 active (.json)
- Tests: 119

## Project Structure Pattern
C# 콘솔 앱 (패킷 캡처/재현/BT) + Python AgentCore (프로토콜 자동 생성, AWS 배포)

## Last Updated
2026-04-01
