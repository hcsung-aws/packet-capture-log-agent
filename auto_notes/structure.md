# Architecture

## 데이터 흐름

### 캡처 모드
```
네트워크 → Raw Socket → ProcessPacket()
  → IP/TCP 헤더 파싱 → 포트 필터링
  → TcpStreamManager.GetOrCreate() → TcpStream.Append()
  → PacketParser.TryParse(stream)
    → 헤더 읽기 (size, type) → Transform 파이프라인 → 필드 파싱
  → PacketFormatter.Format() → 콘솔 + 로그 파일
```

### 재현 모드
```
로그 파일 → PacketReplayer.ParseLog() (정규식 기반)
  → List<ReplayPacket> (Name, Direction, Fields, Timestamp)
  → ReplayWithParsing()
    → SEND 패킷만 순회
    → PacketBuilder.Build() → TCP 소켓 전송
    → 응답 수신 → PacketParser.TryParse() → 콘솔 출력
```

### 분석 → BT/FSM 생성
```
로그 파일 → PacketReplayer.ParseLog() → List<ReplayPacket>
  → SequenceAnalyzer.Analyze() → 분류 + Phase + DynamicField
  → ActionCatalogBuilder.Build() → Action 단위 분할 + merge
  → RecordingStore.Save() → recordings/{protocol}_{client}.json

녹화 → BehaviorTreeBuilder.Build() → BT JSON (explore phase 포함)
녹화 → FsmBuilder.Build() → FSM 전이 확률 JSON
```

### BT 실행
```
BT JSON → BehaviorTreeExecutor.Execute()
  → 노드 순회 (Sequence/Selector/Repeat/Condition/Action)
  → ConditionEvaluator: state expression 평가
  → ActionExecutor: PacketBuilder.Build() → 전송 → 응답 수신/파싱
  → GameWorldState 업데이트
```

### FSM 실행 (부하 테스트)
```
FSM JSON → FsmExecutor.RunAsync()
  → 상태 전이 (확률 기반 랜덤)
  → 각 상태에서 액션 실행 (ActionExecutor)
  → 접속/종료 사이클 반복

LoadTestRunner: N개 클라이언트 × FsmExecutor (Task.Run, async)
```

### 프로토콜 자동 생성 (AgentCore)
```
소스 코드 → CLI/웹 UI → API Gateway (API Key 인증)
  → Orchestrator Lambda
    → source.zip 언팩 → S3 개별 파일 업로드
    → Step Functions 실행 (5 Phase 파이프라인)
      → Discovery → Analysis → Generation → Merge → Validation
      → 각 Phase: Generator + Reviewer (멀티 에이전트 검증)
  → 결과 JSON 다운로드 (presigned URL)
```

## 컴포넌트 의존 관계
```
Program.cs (Composition Root)
  ├── 캡처: CaptureSession ← PacketParser, PacketFormatter, TcpStreamManager
  ├── 재현: PacketReplayer ← PacketBuilder, PacketParser, IReplayInterceptor
  ├── 분석: SequenceAnalyzer → ActionCatalogBuilder → RecordingStore
  ├── BT: BehaviorTreeBuilder → BehaviorTreeExecutor ← ConditionEvaluator, ActionExecutor
  │     └── BehaviorTreeEditor / BehaviorTreeWebEditor (편집)
  ├── FSM: FsmBuilder → FsmExecutor ← ActionExecutor
  │     └── LoadTestRunner (다중 클라이언트)
  └── 공통: Protocol, GameWorldState, FieldFlattener, ReplayLogger

IReplayInterceptor
  ├── NpcAttackInterceptor ← ProximityInterceptor (FindBestPos)
  └── ScenarioBuilder.DynamicFieldInterceptor

IPacketTransform
  ├── RsaDecryptor, XteaDecryptor
  └── TransformFactory (생성)

AgentCore (독립 — AWS 클라우드)
  ├── Orchestrator → Step Functions → 5 Phase Lambda
  ├── Authorizer (API Key)
  └── Client: cli.py / web/index.html → API Gateway
```

## 핵심 모델
- **ProtocolDefinition**: 최상위 JSON 모델 (protocol, transforms, types, packets)
- **PacketDefinition**: 개별 패킷 (type, name, fields)
- **FieldDefinition**: 필드 (name, type, length, count_field, element)
- **ParsedPacket**: 파싱 결과 (Name, Type, Fields dict, RawData)
- **ReplayPacket**: 로그 파싱 결과 (Name, Direction, Fields, Timestamp)
- **BtNode**: BT 노드 (Type, Name, Children, Condition, Action 등)
- **FsmState/FsmTransition**: FSM 상태/전이 확률

## Last Updated
2026-04-06, Mickey 21
