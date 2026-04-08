# AgentCore Architecture

## 개요
게임 소스코드를 입력받아 PacketCaptureAgent용 프로토콜 JSON을 자동 생성하는 LLM 멀티 에이전트 시스템.

## 아키텍처

```
[로컬 CLI/웹] ──HTTP──→ [API Gateway] → [Step Functions]
                                            │
                    ┌───────────────────────┤
                    ↓                       ↓
              [Discovery λ]          [Analysis λ] ×N (Map, 병렬3)
                    │                       │
                    ↓                       ↓
              [Merge λ] ←──────────────────┘
                    │
                    ↓
              [Generation λ]
                    │
                    ↓
              [Validation λ]
                    │
                    ↓
              [S3: protocol.json]
```

## 5 Phase 파이프라인

| Phase | Lambda | 입력 | 출력 | 역할 |
|-------|--------|------|------|------|
| 1. Discovery | discovery | S3 소스 파일 | discovery.json | 패킷 관련 파일 식별 + 언어/프레임워크 판단 |
| 2. Analysis | analysis (Map) | 파일별 소스 | analysis/*.json | 파일별 메타데이터 추출 (구조체, enum, 상수) |
| 3. Merge | merge | analysis/*.json | metadata.json | 통합 메타데이터 (중복 제거, 참조 해결, 매핑) |
| 4. Generation | generation | metadata.json + PROTOCOL_SCHEMA.md | protocol.json | 프로토콜 JSON 생성 (타입 매핑, 배열 처리) |
| 5. Validation | validation | protocol.json | protocol.json (수정) | 스키마 검증 + LLM 자동 수정 (최대 2회) |

## Generator + Reviewer 2-Agent 패턴

각 Phase 내부에서 상호 검증:
```
Generator (Claude) → JSON 결과 생성
    ↓
Reviewer (Claude) → 검증 (approved: true/false + feedback)
    ↓ 불일치 시
Generator → 피드백 반영하여 재생성 (최대 3라운드)
```

## AWS 인프라 (Terraform)

| 리소스 | 용도 |
|--------|------|
| S3 | 작업 데이터 (소스, 중간 결과, 최종 JSON). 30일 lifecycle |
| Lambda ×5 | Phase별 처리. Layer로 공통 모듈 공유 |
| Lambda (orchestrator) | API Gateway → Step Functions 연결 |
| Lambda Layer | llm_client.py, s3_helper.py, prompt_loader.py, prompts/ |
| Step Functions | 파이프라인 오케스트레이션. Analysis는 Map state 병렬 |
| API Gateway (HTTP) | REST 엔드포인트. CORS 설정 |

## 클라이언트

### CLI (agent-core/client/cli.py)
```bash
python3 cli.py generate --source /path/to/source --output protocol.json
python3 cli.py generate --source /path --model us.anthropic.claude-3-5-haiku-20241022-v1:0
python3 cli.py status
python3 cli.py download --job-id <id>
```

### 웹 UI (agent-core/client/app.py)
```bash
python3 app.py 8090  # http://localhost:8090
```
Flask + 바닐라 HTML/JS. 비동기 파이프라인 실행 + 폴링 + JSON 다운로드.

## 주요 설정

| 환경변수 | 기본값 | 설명 |
|----------|--------|------|
| BEDROCK_MODEL_ID | us.anthropic.claude-sonnet-4-20250514-v1:0 | Bedrock 모델 (inference profile) |
| BEDROCK_MAX_TOKENS | 16384 | 최대 출력 토큰 |
| MAX_REVIEW_ROUNDS | 3 | Reviewer 최대 라운드 |
| S3_BUCKET | (Terraform 생성) | 작업 데이터 버킷 |

## 검증 결과 (mmorpg_simulator)

- 소요 시간: ~7분 (Sonnet 4)
- 생성: 53 packets + 5 types
- 참조 대비: 공통 51개 패킷 모든 필드 완벽 일치
- Reviewer 효과: 배열 크기 상수 오류, element 참조 오류 자동 수정

## Last Updated
2026-04-01

## 소스 경로 규칙 (Mickey 22)
- 소스 경로는 반드시 프로젝트 루트를 지정 (예: `mmorpg_simulator/`, NOT `mmorpg_simulator/GameServer/`)
- 패킷 enum/struct가 공유 디렉토리(Common/ 등)에 있으면 하위 디렉토리만 지정 시 type 번호 전부 오류
- Discovery가 missing_dependencies를 감지하므로, CLI에서 이를 사용자에게 경고하는 개선 필요

## Generation 결정론화 (Mickey 22)
- Generation 단계는 LLM 대신 결정론적 코드로 변환 (metadata → protocol JSON)
- LLM은 semantics 생성에만 사용 (하이브리드)
- phases는 type value의 high byte로 결정론적 생성
