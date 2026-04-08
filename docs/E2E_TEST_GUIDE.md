# E2E 테스트 가이드 — 클라이언트 기반 테스트 플랫폼

사람이 한 번 플레이하면, 그 녹화에서 기능 검증(BT)과 부하 테스트(FSM) 시나리오가 모두 자동 생성됩니다.

```
실제 플레이 캡처 → 분석(녹화+카탈로그) → BT/FSM 자동 생성
                                              ├─ BT → Build Validation (기능 검증)
                                              └─ FSM → 부하 테스트
                                                   ├─ 단일 머신 (~200 클라이언트)
                                                   └─ 멀티 에이전트 (수만 클라이언트)
```

## 전제 조건

| 항목 | 요구사항 |
|------|----------|
| OS | Windows 10/11 |
| 런타임 | .NET 9.0 |
| 패킷 캡처 | [Npcap](https://npcap.com/) 설치 |
| 게임 서버 | [MMORPG Simulator](https://github.com/hcsung-aws/mmorpg-simulator) 실행 중 |
| 네트워크 | 127.0.0.1 사용 불가 → 비-loopback IP 사용 (예: 192.168.x.x) |

### MMORPG Simulator mockdb SP 적용 순서

서버의 `scripts/` 디렉토리에서 아래 순서로 실행:

1. `fix_spAccountLogin.sql`
2. `add_spCharacterCreate.sql`
3. `add_spCharacterList.sql`
4. `attendance_system.sql`
5. `gold_system.sql`

## Step 0: 프로토콜 JSON 생성 (신규 게임)

새 게임을 테스트하려면 먼저 패킷 구조를 정의한 프로토콜 JSON이 필요합니다. 두 가지 방법이 있습니다.

### 방법 A: 자동 생성 (LLM 에이전트)

게임 소스코드에서 패킷 구조를 자동 분석하여 JSON을 생성합니다.

> **중요**: 소스 경로는 패킷 정의(enum/struct)가 포함된 최상위 디렉토리를 지정하세요. 예를 들어 `GameServer/`만 지정하면 `Common/Protocol.h` 같은 공유 헤더가 누락될 수 있습니다. 프로젝트 루트를 지정하는 것을 권장합니다.

```powershell
# 환경 변수 설정 (API Gateway + API Key)
$env:PROTOCOL_AGENT_URL = "https://your-api-gw.execute-api.region.amazonaws.com/prod"
$env:PROTOCOL_AGENT_KEY = "your-api-key"

# CLI로 생성 (프로젝트 루트 지정 권장)
cd agent-core\client
python cli.py generate --source C:\path\to\game\project --output protocol.json

# 또는 웹 UI
python app.py 8090
# → http://localhost:8090 에서 소스 업로드 + 결과 확인
```

인프라 배포가 필요한 경우 `agent-core/terraform/`의 Terraform으로 AWS 리소스(S3, Lambda, Step Functions, API Gateway)를 프로비저닝합니다.

### 방법 B: 수동 작성

게임 소스의 패킷 구조체를 보고 직접 JSON을 작성합니다. 상세 스키마는 **[Protocol Schema Guide](PROTOCOL_SCHEMA.md)** 를 참조하세요.

핵심 체크리스트:
- **endian**: little(대부분) / big
- **header**: `size_field`/`type_field`가 실제 헤더 필드명과 일치해야 함
- **pack**: 소스의 `#pragma pack` 확인 (1이면 패딩 없음)
- **packets**: 각 패킷의 type(숫자), direction(C2S/S2C), fields 정의

```json
{
  "protocol": {
    "name": "Game Name",
    "endian": "little",
    "pack": 1,
    "header": {
      "size_field": "length",
      "type_field": "type",
      "fields": [
        {"name": "length", "type": "uint16", "offset": 0},
        {"name": "type", "type": "uint16", "offset": 2}
      ]
    }
  },
  "packets": [
    {
      "type": 257,
      "name": "CS_LOGIN",
      "direction": "C2S",
      "fields": [
        {"name": "accountId", "type": "string", "length": 32}
      ]
    }
  ]
}
```

> 이미 `protocols/` 디렉토리에 프로토콜 JSON이 있는 게임(예: mmorpg_simulator)은 이 단계를 건너뛰세요.

## Step 1: 빌드

```powershell
cd PacketCaptureAgent
dotnet build -c Release
cd bin\Release\net9.0
```

## Step 2: 패킷 캡처 (실제 플레이 녹화)

게임 클라이언트로 직접 플레이하면서 패킷을 캡처합니다.

```powershell
.\PacketCaptureAgent.exe -p C:\path\to\protocols\mmorpg_simulator.json --port 9000
```

- 네트워크 인터페이스 선택 화면이 나타남 → 게임 트래픽이 지나가는 인터페이스 선택
- 게임 클라이언트로 로그인 → 캐릭터 선택 → 게임 플레이
- `Ctrl+C`로 종료 → `capture_YYYYMMDD_HHMMSS.log` 생성

> **Tip**: 프로토콜 파일은 절대경로를 사용하세요.

## Step 3: 캡처 분석 → 녹화 + 액션 카탈로그

캡처 로그를 분석하여 녹화(recordings)와 액션 카탈로그(actions)를 생성합니다.

```powershell
# 단일 로그 분석
.\PacketCaptureAgent.exe -p C:\path\to\protocols\mmorpg_simulator.json --analyze capture.log

# 여러 로그 일괄 분석
.\analyze_all.ps1 C:\path\to\protocols\mmorpg_simulator.json C:\path\to\captures\archive
```

출력 파일:
- `recordings/` — 클라이언트별 분리된 녹화 JSON
- `actions/` — 액션 카탈로그 JSON

## Step 4: BT 생성 → Build Validation (기능 검증)

녹화에서 Behavior Tree를 자동 생성하고, 서버에 대해 실행하여 주요 기능을 검증합니다.

```powershell
# BT 자동 생성
.\PacketCaptureAgent.exe -p C:\path\to\protocols\mmorpg_simulator.json --build-behavior

# BT Validation 실행 (모든 분기를 1회씩 실행)
.\PacketCaptureAgent.exe -p C:\path\to\protocols\mmorpg_simulator.json ^
  --behavior behaviors\mmorpg_simulator_auto.json -t 192.168.x.x:9000
```

용도: 서버 빌드 후 로그인 → 캐릭터 선택 → 입장 → 전투 등 주요 기능이 정상 동작하는지 자동 검증

### BT 편집 (선택)

```powershell
# 웹 에디터 (브라우저 GUI)
.\PacketCaptureAgent.exe --web-editor behaviors\mmorpg_simulator_auto.json --web-port 8080
# → http://localhost:8080

# CLI 편집
.\PacketCaptureAgent.exe --edit-behavior behaviors\mmorpg_simulator_auto.json
```

## Step 5: FSM 생성 → 부하 테스트 (단일 머신)

녹화에서 FSM 전이 확률을 추출하고, 확률 기반 랜덤 행동으로 부하를 생성합니다.

```powershell
# FSM 전이 확률 생성
.\PacketCaptureAgent.exe -p C:\path\to\protocols\mmorpg_simulator.json --build-fsm

# FSM 실행 (120초간 부하)
.\PacketCaptureAgent.exe -p C:\path\to\protocols\mmorpg_simulator.json ^
  --fsm behaviors\mmorpg_simulator_fsm.json -t 192.168.x.x:9000 --duration 120
```

용도: 실제 유저 행동 패턴을 확률적으로 재현. 단일 머신에서 ~100-200 동시 클라이언트.

## Step 6: 멀티 에이전트 → 대규모 부하 테스트 (분산)

여러 머신에 에이전트를 분산 배치하여 수만 동시 클라이언트 규모의 부하를 생성합니다.

### 6-1. 각 머신에서 에이전트 시작

프로토콜 + 시나리오 파일이 각 머신 로컬에 있어야 합니다.

```powershell
.\PacketCaptureAgent.exe --agent-mode -p C:\path\to\protocols\mmorpg_simulator.json --agent-port 8090
```

### 6-2. agents.json 준비

```json
[
  {"url": "http://10.0.1.1:8090"},
  {"url": "http://10.0.1.2:8090"},
  {"url": "http://10.0.1.3:8090"}
]
```

### 6-3. 제어 머신에서 매니저 실행

```powershell
.\PacketCaptureAgent.exe --manager agents.json -t 192.168.x.x:9000 ^
  -s behaviors\mmorpg_simulator_auto.json --clients 2000
```

매니저가 에이전트별로 클라이언트 수를 균등 분배하고, 결과를 수집/집계합니다.

## 빠른 참조: 전체 커맨드 요약

| 단계 | 커맨드 | 용도 |
|------|--------|------|
| 프로토콜 생성 | `cli.py generate --source ... --output ...` | 소스 → JSON 프로토콜 |
| 캡처 | `--port 9000` | 실제 플레이 녹화 |
| 분석 | `--analyze capture.log` | 녹화 + 카탈로그 생성 |
| BT 생성 | `--build-behavior` | 기능 검증용 BT 생성 |
| BT 실행 | `--behavior bt.json -t host:port` | Build Validation |
| FSM 생성 | `--build-fsm` | 부하 테스트용 FSM 생성 |
| FSM 실행 | `--fsm fsm.json -t host:port --duration N` | 부하 테스트 |
| 에이전트 | `--agent-mode --agent-port 8090` | 분산 에이전트 |
| 매니저 | `--manager agents.json --clients N` | 대규모 부하 |

## 제한사항

- TCP만 지원 (UDP 미지원)
- 암호화된 패킷 미지원
- 127.0.0.1 loopback 캡처 불가 (Windows Raw Socket 제한)
- 프로토콜 JSON의 `size_field`/`type_field`가 실제 헤더 필드명과 일치해야 함
