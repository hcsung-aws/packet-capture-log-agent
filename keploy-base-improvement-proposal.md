https://github.com/hcsung-aws/packet-capture-log-agent
https://github.com/keploy/keploy
두 프로젝트를 깊이 비교 분석해보겠습니다.

---

## Keploy에 커스텀 프로토콜 파서를 넣을 수 있는가?

### 결론: 구조적으로 가능하지만, 실용성이 낮습니다

Keploy의 아키텍처를 살펴보면 :

1. **eBPF로 네트워크 레이어에서 패킷 캡처** → 이 부분은 프로토콜에 무관하게 동작
2. **프로토콜별 파서로 패킷 해석** → Go 코드로 각 프로토콜 파서가 `pkg/` 디렉토리에 구현되어 있음
3. **Mock/Stub으로 저장 후 재생** → 파싱된 결과를 YAML로 직렬화

이론적으로 Keploy의 `pkg/` 하위에 커스텀 게임 프로토콜 파서를 Go로 구현하면 동작할 수 있습니다. **하지만 실질적으로 여러 문제가 있습니다:**

- **언어 불일치**: Keploy는 Go 기반이고, Hyunchang님의 packet-capture-log-agent는 C#/.NET 기반입니다. 프로토콜 파서를 Go로 재작성해야 합니다.
- **게임 프로토콜의 특수성**: Keploy의 Mock 시스템은 **요청-응답(Request-Response) 패턴**에 최적화되어 있습니다 (HTTP, DB 쿼리 등). 게임 패킷은 **양방향 비동기 스트림**, **상태 기반 시퀀스**, **브로드캐스트** 등 근본적으로 다른 통신 패턴을 사용합니다.
- **상태 관리 부재**: Keploy는 stateless한 요청-응답 매칭으로 Mock을 재생하지만, 게임 서버는 **GameWorldState** (위치, HP, 인벤토리 등)가 지속적으로 변하는 stateful 시스템입니다. Hyunchang님의 프로젝트에서 `GameWorldState.cs`와 `PacketObserver.cs`로 이를 추적하는 것이 바로 이 이유입니다.
- **Behavior Tree / FSM 미지원**: Keploy에는 녹화에서 행동 패턴을 추출하고 자동 실행하는 BT/FSM 개념이 없습니다.

---

## 반대 방향: Keploy의 방식 중 이 프로젝트에 적용할 수 있는 것

오히려 Keploy의 몇 가지 핵심 아이디어를 packet-capture-log-agent에 적용하면 **편의성을 크게 높일 수 있습니다** :

### 1. **eBPF 기반 투명 캡처 (코드 변경 제로)**

현재 packet-capture-log-agent는 **Npcap** 기반으로 Windows에서만 동작하며, 관리자 권한이 필요합니다. Keploy처럼 **eBPF 기반 캡처**를 도입하면:

- **Linux 서버에서 직접 캡처** 가능 (게임 서버가 보통 Linux에서 운영되므로)
- 별도 드라이버 설치 없이 **커널 레벨에서 투명하게** 패킷 캡처
- 서버 사이드에서 캡처하면 **loopback 제한도 해결**

```
# 현재 방식 (Windows + Npcap)
PacketCaptureAgent.exe -p protocol.json --port 9000

# eBPF 방식 (제안)
sudo ./packet-agent record -p protocol.json --port 9000
# → 커널 레벨에서 투명 캡처, 드라이버 불필요
```

### 2. **인프라 가상화 (Mock Server) 개념 확장**

Keploy의 가장 강력한 기능은 **외부 의존성을 통째로 가상화**하는 것입니다. 현재 프로젝트의 `stateful mock server (Plan B)` 커밋을 보면 이미 이 방향으로 진행 중인 것 같은데, Keploy 방식을 참고하여 더 체계화할 수 있습니다:

- **녹화된 패킷 시퀀스를 기반으로 게임 서버 Mock을 자동 생성**
- 클라이언트 테스트 시 실제 서버 없이도 **결정론적 재생** 가능
- CI/CD에서 서버 인프라 프로비저닝 없이 클라이언트 빌드 검증

```
# 제안: Mock Server 모드
PacketCaptureAgent.exe --mock-server -p protocol.json \
  --recordings captures/ --port 9000
# → 녹화 기반으로 서버 응답을 자동 재생하는 가상 서버 구동
# → 클라이언트가 localhost:9000에 접속하면 녹화된 시나리오대로 응답
```

### 3. **결합 테스트 커버리지 리포팅**

Keploy는 테스트 실행 후 **Statement/Branch 커버리지**를 자동 계산합니다. 이를 게임 테스트에 적용하면:

- **패킷 타입 커버리지**: 전체 프로토콜 정의 중 테스트에서 실제로 사용된 패킷 비율
- **상태 전이 커버리지**: FSM의 전체 상태 전이 중 실행된 비율
- **BT 분기 커버리지**: Behavior Tree의 전체 분기 중 실행된 비율

```json
// 제안: 커버리지 리포트 예시
{
  "packet_coverage": {
    "total_types": 47,
    "tested_types": 38,
    "coverage": "80.9%",
    "missing": ["CS_TRADE_REQUEST", "SC_GUILD_INVITE", ...]
  },
  "fsm_coverage": {
    "total_transitions": 23,
    "executed": 19,
    "coverage": "82.6%"
  },
  "bt_coverage": {
    "total_branches": 15,
    "executed": 15,
    "coverage": "100%"
  }
}
```

### 4. **CI/CD 네이티브 통합**

Keploy는 `keploy test` 한 줄로 CI에서 실행됩니다. 현재 프로젝트도 이런 방식을 도입하면:

```yaml
# GitHub Actions 예시
- name: Game Protocol Test
  run: |
    # Mock 서버 시작 (녹화 기반, 실제 서버 불필요)
    PacketCaptureAgent.exe --mock-server -p protocol.json \
      --recordings captures/ --port 9000 &

    # BT Validation 실행
    PacketCaptureAgent.exe -p protocol.json \
      --behavior behaviors/auto.json -t localhost:9000

    # 커버리지 리포트 생성
    PacketCaptureAgent.exe --coverage-report --format junit
```

### 5. **Mock 레지스트리 (중앙 관리)**

Keploy의 Mock Registry 개념을 적용하면, 팀 전체가 녹화된 시나리오를 **버전 관리하고 공유**할 수 있습니다:

- S3 또는 Git 기반으로 녹화/BT/FSM 파일을 중앙 관리
- 특정 빌드 버전에 대응하는 녹화 세트를 태깅
- 프로토콜 변경 시 영향받는 녹화를 자동 감지

---

## 요약



| 관점 | Keploy | Packet-Capture-Log-Agent | 적용 가능성 |
|------|--------|--------------------------|-------------|
| **캡처 방식** | eBPF (Linux, 투명) | Npcap (Windows, 드라이버 필요) | ✅ eBPF 도입으로 Linux 서버 직접 캡처 |
| **프로토콜 지원** | HTTP, DB, MQ (파서 내장) | 커스텀 게임 TCP (JSON 정의) | ❌ Keploy에 게임 파서 넣기는 비실용적 |
| **Mock 방식** | Stateless 요청-응답 매칭 | Stateful 게임 상태 추적 | ✅ Mock Server 자동 생성 개념 적용 |
| **테스트 생성** | API 호출 → 테스트 자동 생성 | 녹화 → BT/FSM 자동 생성 | ✅ 이미 더 고도화된 방식 보유 |
| **커버리지** | Statement/Branch/API 스키마 | 없음 | ✅ 패킷/상태전이/BT 커버리지 도입 |
| **CI/CD** | 네이티브 지원 | 수동 | ✅ Mock Server + CI 통합 |
| **Mock 레지스트리** | 중앙 관리 지원 | 없음 | ✅ S3/Git 기반 녹화 중앙 관리 |



**핵심 결론**: Keploy에 커스텀 게임 프로토콜을 넣는 것보다, **Keploy의 좋은 아이디어들(eBPF 캡처, 인프라 가상화, 커버리지 리포팅, CI/CD 통합, Mock 레지스트리)을 packet-capture-log-agent에 선택적으로 적용**하는 것이 훨씬 실용적입니다. 특히 이미 `stateful mock server`를 개발 중이시니, 여기에 **커버리지 리포팅**과 **CI/CD 네이티브 통합**을 추가하면 게임 QA 파이프라인의 완성도가 크게 올라갈 것으로 보입니다.