# 프로토콜 자동 생성 에이전트 — 배포 가이드

게임 소스코드에서 패킷 프로토콜 JSON을 자동 생성하는 LLM 에이전트 파이프라인을 AWS 계정에 배포하는 방법입니다.

## 아키텍처

```
CLI/웹 → API Gateway (HTTP API + API Key) → Orchestrator Lambda
  → Step Functions: Discovery → Analysis(Map) → Merge → Generation → Validation
  → S3 (소스 업로드, 중간 결과, 최종 protocol.json)
  → Bedrock (Claude Sonnet)
```

## 사전 요구사항

- AWS 계정 + CLI 설정 (`aws configure`)
- Terraform ≥ 1.5
- Python 3.12+ (CLI 클라이언트용)
- Bedrock 모델 접근 권한 (해당 리전에서 Claude Sonnet 활성화 필요)

### Bedrock 모델 활성화

1. AWS Console → Amazon Bedrock → Model access
2. `Anthropic Claude Sonnet` 모델 접근 요청 + 승인 대기
3. 승인 후 inference profile ID 확인 (예: `us.anthropic.claude-sonnet-4-20250514-v1:0`)

## 배포

```bash
# 1. 리포지토리 클론
git clone https://github.com/hcsung-aws/packet-capture-log-agent.git
cd packet-capture-log-agent/agent-core/terraform

# 2. 변수 설정 (선택 — 기본값 사용 가능)
#    terraform.tfvars 파일 생성 또는 -var 플래그 사용
cat > terraform.tfvars <<EOF
aws_region       = "us-east-1"
project_name     = "protocol-agent"
bedrock_model_id = "us.anthropic.claude-sonnet-4-20250514-v1:0"
EOF

# 3. 배포
terraform init
terraform apply
```

배포 완료 시 출력:
```
api_endpoint = "https://xxxxx.execute-api.us-east-1.amazonaws.com"
api_key      = <sensitive>
s3_bucket    = "protocol-agent-jobs-123456789012"
```

API Key 확인:
```bash
terraform output -raw api_key
```

## 사용

### CLI

```bash
cd packet-capture-log-agent/agent-core/client
pip install requests

# 환경 변수 설정
export PROTOCOL_AGENT_URL="https://xxxxx.execute-api.us-east-1.amazonaws.com"
export PROTOCOL_AGENT_KEY="your-api-key"

# 프로토콜 생성 (소스 디렉토리는 프로젝트 루트 권장)
python3 cli.py generate --source /path/to/game/project --output protocol.json
```

### 웹 UI

```bash
cd packet-capture-log-agent/agent-core/client
pip install requests flask
python3 app.py 8090
# http://localhost:8090
```

## 변수 참조

| 변수 | 기본값 | 설명 |
|------|--------|------|
| `aws_region` | `us-east-1` | 배포 리전 |
| `project_name` | `protocol-agent` | 리소스 이름 접두사 |
| `bedrock_model_id` | `us.anthropic.claude-sonnet-4-20250514-v1:0` | Bedrock 모델 ID |

## 삭제

```bash
cd agent-core/terraform
terraform destroy
```

## 주의사항

- 소스 경로는 프로젝트 루트를 지정해야 합니다. 하위 디렉토리만 지정하면 공유 헤더/타입이 누락되어 정확도가 떨어집니다 (CLI에서 경고 표시).
- Bedrock 모델 접근 권한이 없으면 Lambda가 실패합니다. 배포 전 모델 활성화를 확인하세요.
- S3 버킷의 job 데이터는 30일 후 자동 삭제됩니다.

---

# Protocol Auto-Generation Agent — Deployment Guide

Deploy the LLM agent pipeline that auto-generates packet protocol JSON from game source code.

## Architecture

```
CLI/Web → API Gateway (HTTP API + API Key) → Orchestrator Lambda
  → Step Functions: Discovery → Analysis(Map) → Merge → Generation → Validation
  → S3 (source upload, intermediate results, final protocol.json)
  → Bedrock (Claude Sonnet)
```

## Prerequisites

- AWS account + CLI configured (`aws configure`)
- Terraform ≥ 1.5
- Python 3.12+ (for CLI client)
- Bedrock model access (enable Claude Sonnet in your region)

### Enable Bedrock Model

1. AWS Console → Amazon Bedrock → Model access
2. Request access for `Anthropic Claude Sonnet` → wait for approval
3. Note the inference profile ID (e.g., `us.anthropic.claude-sonnet-4-20250514-v1:0`)

## Deploy

```bash
cd packet-capture-log-agent/agent-core/terraform

# Optional: customize variables
cat > terraform.tfvars <<EOF
aws_region       = "us-east-1"
project_name     = "protocol-agent"
bedrock_model_id = "us.anthropic.claude-sonnet-4-20250514-v1:0"
EOF

terraform init
terraform apply
```

Get API key:
```bash
terraform output -raw api_key
```

## Usage

```bash
cd agent-core/client
pip install requests

export PROTOCOL_AGENT_URL="https://xxxxx.execute-api.us-east-1.amazonaws.com"
export PROTOCOL_AGENT_KEY="your-api-key"

python3 cli.py generate --source /path/to/game/project --output protocol.json
```

## Teardown

```bash
terraform destroy
```
