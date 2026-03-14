# FILE-STRUCTURE

## Directory Tree
```
packet-capture-log-agent/
├── PacketCaptureAgent/          # 메인 소스
│   ├── Program.cs               # 진입점 (캡처/재현 모드 분기)
│   ├── Protocol.cs              # JSON 프로토콜 정의 모델 (9개 클래스)
│   ├── PacketParser.cs          # TCP 스트림 → 동적 패킷 파싱
│   ├── PacketBuilder.cs         # 파싱 데이터 → 바이너리 패킷 재구성
│   ├── PacketReplayer.cs        # 로그 파싱 + TCP 재전송
│   ├── PacketFormatter.cs       # 콘솔/파일 출력 포맷
│   ├── TcpStream.cs             # TCP 스트림 재조립 + 연결 관리
│   ├── IPacketTransform.cs      # 패킷 변환 인터페이스 + 팩토리
│   ├── RsaDecryptor.cs          # RSA 복호화 (Tibia용)
│   ├── XteaDecryptor.cs         # XTEA 복호화 (Tibia용)
│   └── PacketCaptureAgent.csproj
├── protocols/                   # 프로토콜 정의 JSON
│   ├── echoclient.json          # 에코 테스트용
│   ├── mmorpg_simulator.json    # MMORPG 시뮬레이터 (23 packets)
│   └── tibia.json               # Tibia/ForgottenServer (RSA+XTEA)
├── docs/
│   └── PROTOCOL_SCHEMA.md       # 프로토콜 JSON 스키마 가이드
├── context_rule/                # 프로젝트 특화 규칙
├── common_knowledge/            # 범용 재사용 패턴
└── auto_notes/                  # 자동 관찰 메모
```

## Key Files
- Config: PacketCaptureAgent.csproj (.NET 9.0, no dependencies)
- Entry: Program.cs (Raw Socket 캡처 + CLI 인자 파싱)
- Docs: docs/PROTOCOL_SCHEMA.md, README.md

## File Statistics
- Source files: 10 (.cs)
- Protocol definitions: 3 (.json)
- Capture logs: ~15 (.log, gitignored)

## Project Structure Pattern
Single-project console app, 모듈별 단일 파일 구조

## Last Updated
2026-03-11
