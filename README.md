# Packet Capture Log Agent

온라인 게임의 TCP 패킷을 캡처하고, 프로토콜 정의에 따라 파싱하여 재현 가능한 로그를 생성하는 도구입니다.

**관련 프로젝트:** [MMORPG Simulator](https://github.com/hcsung-aws/mmorpg-simulator) - 이 도구로 테스트할 수 있는 게임 시뮬레이터

[English](#english)

## 개요

게임 클라이언트의 네트워크 패킷을 캡처하고, JSON으로 정의된 프로토콜에 따라 패킷을 파싱합니다. 생성된 로그를 사용하여 패킷 통신을 재현할 수 있습니다.

## 기능

- **패킷 캡처**: 특정 포트의 TCP 패킷 캡처
- **프로토콜 파싱**: JSON 기반 동적 패킷 파싱
- **패킷 재현**: 캡처된 로그를 사용한 패킷 재전송 (타이밍 기반 딜레이)
- **인터셉터**: 재현 중 패킷을 동적으로 수정 (예: NPC 공격 대상 자동 교체)
- **Behavior Tree**: 캡처 녹화에서 자동 생성, Build Validation 실행, 웹 에디터
- **FSM (부하 테스트)**: 녹화에서 전이 확률 추출, 확률 기반 랜덤 행동, 접속/종료 사이클
- **멀티 에이전트 매니저**: 여러 머신에 에이전트 분산 배치, 수만 동시 클라이언트 부하 테스트
- **프로토콜 자동 생성**: 게임 소스코드 → LLM 멀티 에이전트 분석 → 결정론적 변환 → JSON 프로토콜 자동 생성
- **지원 타입**: 정수, 문자열, 배열, 구조체, length-prefixed 문자열, 조건부 필드

## 요구사항

- Windows 10/11
- .NET 9.0
- Npcap (패킷 캡처용)

## 빌드

```bash
cd PacketCaptureAgent
dotnet build -c Release
```

## 사용법

### 캡처 모드

```bash
PacketCaptureAgent.exe -p protocol.json --port 9000
```

### 재현 모드

```bash
PacketCaptureAgent.exe -p protocol.json -r capture.log -t host:port
```

### Behavior Tree (Build Validation)

```bash
# 캡처 로그 분석 → 녹화 + 액션 카탈로그 생성 (멀티 클라이언트 자동 분리)
PacketCaptureAgent.exe -p protocol.json --analyze capture.log

# 여러 로그 일괄 분석 (디렉토리 또는 파일 목록)
.\analyze_all.ps1 ..\protocols\mmorpg_simulator.json ..\captures\archive

# 녹화에서 BT 자동 생성
PacketCaptureAgent.exe -p protocol.json --build-behavior

# BT Validation 실행 (모든 분기를 1회씩 실행, 성공/실패 로깅)
PacketCaptureAgent.exe -p protocol.json --behavior behaviors/auto.json -t host:port

# CLI 편집
PacketCaptureAgent.exe --edit-behavior behaviors/auto.json

# 웹 에디터 (브라우저 GUI)
PacketCaptureAgent.exe --web-editor behaviors/auto.json [--web-port 8080]
```

### FSM (부하 테스트)

```bash
# 녹화에서 FSM 전이 확률 생성
PacketCaptureAgent.exe -p protocol.json --build-fsm

# FSM 실행 (확률 기반 랜덤 행동 + 접속/종료 사이클)
PacketCaptureAgent.exe -p protocol.json --fsm behaviors/fsm.json -t host:port --duration 120
```

### 멀티 에이전트 매니저 (대규모 부하 테스트)

```bash
# 1. 각 머신에서 에이전트 시작 (프로토콜+시나리오 파일이 로컬에 있어야 함)
PacketCaptureAgent.exe --agent-mode -p protocol.json --agent-port 8090

# 2. 제어 머신에서 매니저 실행
#    agents.json: [{"url":"http://10.0.1.1:8090"}, {"url":"http://10.0.1.2:8090"}]
PacketCaptureAgent.exe --manager agents.json -t host:port -s behaviors/auto.json --clients 2000
```

### 프로토콜 자동 생성

게임 소스코드에서 패킷 구조를 자동 분석하여 프로토콜 JSON을 생성합니다. 소스 경로는 패킷 정의(enum/struct)가 포함된 디렉토리를 지정해야 합니다.

```bash
# 환경 변수 설정 (API Gateway)
export PROTOCOL_AGENT_URL="https://your-api.execute-api.region.amazonaws.com"
export PROTOCOL_AGENT_KEY="your-api-key"

# CLI (소스 → JSON 프로토콜 자동 생성)
cd agent-core/client
python3 cli.py generate --source /path/to/game/project --output protocol.json

# 웹 UI
python3 app.py 8090  # http://localhost:8090
```

파이프라인: Discovery(파일 식별) → Analysis(구조체/enum 추출, LLM) → Merge(통합, LLM) → Generation(결정론적 변환) → Validation(스키마 검증)

### 옵션

| 옵션 | 설명 |
|------|------|
| `-p, --protocol` | 프로토콜 JSON 파일 경로 |
| `-r, --replay` | 재현할 로그 파일 |
| `-t, --target` | 재현 대상 서버 (host:port) |
| `--port` | 캡처할 포트 |
| `--mode` | 재현 모드 (timing/response/hybrid) |
| `--speed` | 재생 속도 (기본: 1.0) |
| `--analyze` | 캡처 로그 분석 + 녹화/카탈로그 생성 |
| `--build-behavior` | 녹화에서 BT 자동 생성 |
| `--behavior` | BT Validation 실행 |
| `--build-fsm` | 녹화에서 FSM 전이 확률 생성 |
| `--fsm` | FSM 실행 (부하 테스트) |
| `--duration` | FSM/BT 실행 시간 (초, 0=무한) |
| `--edit-behavior` | BT CLI 편집 |
| `--web-editor` | BT 웹 에디터 |
| `--web-port` | 웹 에디터 포트 (기본: 8080) |
| `--agent-mode` | 에이전트 모드 (HTTP API 서버) |
| `--agent-port` | 에이전트 포트 (기본: 8090) |
| `--manager` | 매니저 모드 (agents.json 경로) |

## 프로토콜 JSON 형식

```json
{
  "protocol": {
    "name": "Game Protocol",
    "endian": "little",
    "header": {
      "size_field": "length",
      "type_field": "type",
      "fields": [
        {"name": "length", "type": "uint16"},
        {"name": "type", "type": "uint16"}
      ]
    }
  },
  "packets": [
    {
      "type": "0x0101",
      "name": "CS_LOGIN",
      "direction": "C2S",
      "fields": [
        {"name": "accountId", "type": "string", "length": 32}
      ]
    }
  ]
}
```

## 프로젝트 구조

```
packet-capture-log-agent/
├── PacketCaptureAgent/
│   ├── Program.cs              # 메인 (캡처/재현 모드)
│   ├── Protocol.cs             # JSON 프로토콜 정의
│   ├── PacketParser.cs         # 동적 패킷 파싱
│   ├── PacketBuilder.cs        # 패킷 빌드 (재현용)
│   ├── PacketReplayer.cs       # 로그 파싱 + 재전송
│   ├── PacketFormatter.cs      # 출력 포맷
│   ├── TcpStream.cs            # TCP 스트림 재조립
│   ├── GameWorldState.cs       # 리플레이 중 게임 상태 추적
│   ├── BehaviorTree.cs         # BT 노드 모델 + JSON 직렬화
│   ├── BehaviorTreeBuilder.cs  # 녹화 → BT 자동 생성
│   ├── BehaviorTreeExecutor.cs # BT 런타임 실행
│   ├── BehaviorTreeEditor.cs   # BT CLI 편집기
│   ├── BehaviorTreeWebEditor.cs # BT 웹 에디터 (HttpListener)
│   ├── FsmDefinition.cs        # FSM 전이 확률 모델
│   ├── FsmBuilder.cs           # 녹화 → FSM 전이 확률 생성
│   ├── FsmExecutor.cs          # FSM 런타임 실행
│   ├── IReplayInterceptor.cs   # 인터셉터 인터페이스
│   └── NpcAttackInterceptor.cs # NPC 공격 대상 자동 교체
├── agent-core/                 # 프로토콜 자동 생성 (LLM Agent)
│   ├── poc/                    # 로컬 PoC (Bedrock 직접 호출)
│   ├── lambda/                 # AWS Lambda 함수 (5 Phase + Orchestrator)
│   ├── terraform/              # 인프라 정의 (S3, Lambda, Step Functions, API GW)
│   └── client/                 # CLI + 웹 프론트엔드
├── protocols/
│   ├── echoclient.json
│   └── mmorpg_simulator.json
└── docs/
    ├── BEHAVIOR_TREE_DESIGN.md
    └── PROTOCOL_SCHEMA.md
```

## E2E 테스트 (전체 파이프라인)

캡처 → 분석 → BT/FSM 생성 → 기능 검증/부하 테스트까지의 전체 파이프라인 실행 방법은 **[E2E 테스트 가이드](docs/E2E_TEST_GUIDE.md)** 를 참조하세요.

```
실제 플레이 캡처 → 분석(녹화+카탈로그) → BT/FSM 자동 생성
                                              ├─ BT → Build Validation (기능 검증)
                                              └─ FSM → 부하 테스트
                                                   ├─ 단일 머신 (~200 클라이언트)
                                                   └─ 멀티 에이전트 (수만 클라이언트)
```

## 제한사항

- TCP만 지원 (UDP 미지원)
- 암호화된 패킷 미지원
- 127.0.0.1 loopback 캡처 불가 (Windows 제한)

## 개발 히스토리

이 프로젝트는 [Mickey (AI Developer Agent)](https://github.com/hcsung-aws/ai-developer-mickey)를 활용하여 개발되었습니다. 세션별 작업 기록은 `MICKEY-*-SESSION.md` 파일에서 확인할 수 있습니다.

## 라이선스

MIT License

---

# English

**Related Project:** [MMORPG Simulator](https://github.com/hcsung-aws/mmorpg-simulator) - A game simulator that can be tested with this tool

## Overview

A tool for capturing TCP packets from online games, parsing them according to protocol definitions, and generating reproducible logs.

## Features

- **Packet Capture**: Capture TCP packets on specific ports
- **Protocol Parsing**: Dynamic packet parsing based on JSON definitions
- **Packet Replay**: Resend packets using captured logs (timing-based delay)
- **Interceptor**: Dynamically modify packets during replay (e.g., auto-retarget NPC attacks)
- **Supported Types**: integers, strings, arrays, structs

## Requirements

- Windows 10/11
- .NET 9.0
- Npcap (for packet capture)

## Build

```bash
cd PacketCaptureAgent
dotnet build -c Release
```

## Usage

### Capture Mode

```bash
PacketCaptureAgent.exe -p protocol.json --port 9000
```

### Replay Mode

```bash
PacketCaptureAgent.exe -p protocol.json -r capture.log -t host:port
```

## E2E Testing (Full Pipeline)

See **[E2E Test Guide](docs/E2E_TEST_GUIDE.md)** for the complete pipeline: Capture → Analyze → BT/FSM generation → Build Validation / Load Testing.

## Limitations

- TCP only (no UDP support)
- No encrypted packet support
- Cannot capture 127.0.0.1 loopback (Windows limitation)

## Development History

This project was developed with [Mickey (AI Developer Agent)](https://github.com/hcsung-aws/ai-developer-mickey). Session logs are available in `MICKEY-*-SESSION.md` files.

## License

MIT License
