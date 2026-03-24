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

### 분석 모드
```
로그 파일 → PacketReplayer.ParseLog() → List<ReplayPacket>
  → SequenceAnalyzer.Analyze() → 분류 + Phase + ASCII/Mermaid
  → DetectDynamicFields() → suffix 타입 필터 + 시간 순서
  → ActionCatalogBuilder.Build() → Action 단위 분할
  → ActionCatalogBuilder.MergeAndSave() → actions/{protocol}_actions.json
```

## 컴포넌트 의존 관계
```
Program.cs
  ├── PacketParser ← Protocol, TcpStream, IPacketTransform
  ├── PacketFormatter ← Protocol
  ├── PacketReplayer ← Protocol, PacketBuilder, PacketParser
  │     └── PacketBuilder ← Protocol
  ├── SequenceAnalyzer ← Protocol (분석 모드)
  │     └── ActionCatalogBuilder (카탈로그 빌드 + merge)
  ├── ScenarioBuilder ← ActionCatalog (시나리오 모드, 기존 코드와 완전 분리)
  │     ├── DynamicFieldInterceptor ← IReplayInterceptor
  │     └── TrackingResponseHandler ← IResponseHandler (래핑)
  └── TcpStreamManager → TcpStream

IPacketTransform (인터페이스)
  ├── RsaDecryptor
  └── XteaDecryptor
  └── TransformFactory (생성)
```

## 핵심 모델
- **ProtocolDefinition**: 최상위 JSON 모델 (protocol, transforms, types, packets)
- **PacketDefinition**: 개별 패킷 (type, name, fields)
- **FieldDefinition**: 필드 (name, type, length, count_field, element)
- **ParsedPacket**: 파싱 결과 (Name, Type, Fields dict, RawData)
- **ReplayPacket**: 로그 파싱 결과 (Name, Direction, Fields, Timestamp)
- **ConnectionKey**: TCP 연결 식별 (SrcIP:Port → DstIP:Port)

## 프로토콜 JSON 지원 기능
- 헤더: 동적 필드 오프셋 기반 + 자동 크기 계산
- 타입: int8~64, uint8~64, float, double, bool, string, bytes
- 고급: array (count_field/length), struct (커스텀 타입), enum
- 엔디안: little/big
- Transform: RSA, XTEA (파이프라인 체이닝)
