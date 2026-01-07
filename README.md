# Packet Capture Log Agent

온라인 게임의 TCP 패킷을 캡처하고, 프로토콜 정의에 따라 파싱하여 재현 가능한 로그를 생성하는 도구입니다.

[English](#english)

## 개요

게임 클라이언트의 네트워크 패킷을 캡처하고, JSON으로 정의된 프로토콜에 따라 패킷을 파싱합니다. 생성된 로그를 사용하여 패킷 통신을 재현할 수 있습니다.

## 기능

- **패킷 캡처**: 특정 포트의 TCP 패킷 캡처
- **프로토콜 파싱**: JSON 기반 동적 패킷 파싱
- **패킷 재현**: 캡처된 로그를 사용한 패킷 재전송
- **지원 타입**: 정수, 문자열, 배열, 구조체

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

### 옵션

| 옵션 | 설명 |
|------|------|
| `-p, --protocol` | 프로토콜 JSON 파일 경로 |
| `-r, --replay` | 재현할 로그 파일 |
| `-t, --target` | 재현 대상 서버 (host:port) |
| `--port` | 캡처할 포트 |
| `--mode` | 재현 모드 (timing/response/hybrid) |
| `--speed` | 재생 속도 (기본: 1.0) |

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
│   ├── Program.cs          # 메인 (캡처/재현 모드)
│   ├── Protocol.cs         # JSON 프로토콜 정의
│   ├── PacketParser.cs     # 동적 패킷 파싱
│   ├── PacketBuilder.cs    # 패킷 빌드 (재현용)
│   ├── PacketReplayer.cs   # 로그 파싱 + 재전송
│   ├── PacketFormatter.cs  # 출력 포맷
│   └── TcpStream.cs        # TCP 스트림 재조립
└── protocols/
    ├── echoclient.json
    └── mmorpg_simulator.json
```

## 제한사항

- TCP만 지원 (UDP 미지원)
- 암호화된 패킷 미지원
- 127.0.0.1 loopback 캡처 불가 (Windows 제한)

## 라이선스

MIT License

---

# English

## Overview

A tool for capturing TCP packets from online games, parsing them according to protocol definitions, and generating reproducible logs.

## Features

- **Packet Capture**: Capture TCP packets on specific ports
- **Protocol Parsing**: Dynamic packet parsing based on JSON definitions
- **Packet Replay**: Resend packets using captured logs
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

## Limitations

- TCP only (no UDP support)
- No encrypted packet support
- Cannot capture 127.0.0.1 loopback (Windows limitation)

## License

MIT License
