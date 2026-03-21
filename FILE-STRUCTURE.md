# FILE-STRUCTURE

## Directory Tree
```
packet-capture-log-agent/
├── PacketCaptureAgent/          # 메인 소스
│   ├── Program.cs               # 진입점 (캡처/재현 모드 분기)
│   ├── Protocol.cs              # JSON 프로토콜 정의 모델
│   ├── PacketParser.cs          # TCP 스트림 → 동적 패킷 파싱
│   ├── PacketBuilder.cs         # 파싱 데이터 → 바이너리 패킷 재구성
│   ├── PacketReplayer.cs        # 로그 파싱 + TCP 재전송 (IResponseHandler)
│   ├── PacketFormatter.cs       # 콘솔/파일 출력 포맷
│   ├── TcpStream.cs             # TCP 스트림 재조립 + 연결 관리
│   ├── GameWorldState.cs        # 리플레이 중 게임 상태 추적
│   ├── IReplayInterceptor.cs    # 인터셉터 인터페이스 + ReplayContext
│   ├── NpcAttackInterceptor.cs  # NPC 공격 동적 대상 교체
│   ├── IPacketTransform.cs      # 패킷 변환 인터페이스 + 팩토리
│   ├── RsaDecryptor.cs          # RSA 복호화 (Tibia용)
│   └── XteaDecryptor.cs         # XTEA 복호화 (Tibia용)
├── protocols/                   # 프로토콜 정의 JSON
│   ├── echoclient.json          # 에코 테스트용
│   ├── mmorpg_simulator.json    # MMORPG 시뮬레이터 (36 packets)
│   └── tibia.json               # Tibia/ForgottenServer (RSA+XTEA)
├── prototype/                   # Phase 3 분석/설계
│   ├── analyze_scenario.py      # 패킷 시퀀스 분석 스크립트
│   ├── PHASE3_PLAN.md           # TestPlay BT 기반 설계
│   └── ...                      # 분석 결과, 패킷 분류 문서
├── docs/
│   └── PROTOCOL_SCHEMA.md       # 프로토콜 JSON 스키마 가이드
├── sessions/                    # 아카이빙된 세션 문서 (Mickey 1~6)
├── context_rule/                # 프로젝트 특화 규칙
├── common_knowledge/            # 범용 재사용 패턴
└── auto_notes/                  # 자동 관찰 메모
```

## Key Files
- Config: PacketCaptureAgent.csproj (.NET 9.0, no dependencies)
- Entry: Program.cs (Raw Socket 캡처 + CLI 인자 파싱 + 인터셉터 연결)
- Docs: docs/PROTOCOL_SCHEMA.md, README.md

## File Statistics
- Source files: 13 (.cs)
- Protocol definitions: 3 (.json)
- Session archives: 10 (sessions/)

## Project Structure Pattern
Single-project console app, 모듈별 단일 파일 구조. 인터셉터 패턴으로 리플레이 확장.

## Last Updated
2026-03-19
