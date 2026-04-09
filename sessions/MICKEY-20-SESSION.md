# MICKEY-20-SESSION

## Session Meta
- Type: Implementation
- Date: 2026-04-06

## Session Goal
B 단계 진행 — AgentCore 개선 (API Gateway 정상화 + 인증)

## Purpose Alignment
- 기여 시나리오: Phase 1 (프로토콜 자동 생성) — 고객이 실제 사용할 수 있는 서비스 형태로 전환
- 이번 세션 범위: B-1 Discovery 프롬프트 개선, B-2a Orchestrator 정상화, B-2b API Key 인증

## Previous Context
Mickey 19: A 단계(품질 개선) 완료. Characterization tests 25개, 공통 유틸 추출/중복 제거, BT Builder/Executor 테스트 18개. 162개 전체 통과. A/B/C 로드맵 확정.

## Current Tasks
- [x] A-3 완료 확인 | CC: Builder 7 + Executor 11 = 18개, 순수 함수 전부 커버
- [x] B-1: Discovery 프롬프트 개선 | CC: generator 제외 기준 6항목, reviewer 힌트 추가
- [x] B-2a: Orchestrator zip 언팩 + API Gateway 경로 정상화 | CC: v2 이벤트 파싱 + zip 언팩 + Terraform 조정
- [x] B-2b: API Key 인증 추가 | CC: authorizer Lambda + Terraform 설정
- [x] B-2c: CLI 전환 (boto3 → API Gateway HTTP) | CC: requests 기반, AWS SDK 불필요
- [x] B-2d: 웹 UI 전환 (프론트엔드 → API Gateway 직접) | CC: SPA, Flask 제거

## Progress

### Completed
- A-3 완료 확인 (Builder 7 + Executor 11, 162개 전체 통과)
- B-1: Discovery 프롬프트 개선 (generator 제외 기준 6항목, reviewer 힌트)
- B-2a: Orchestrator 전면 수정:
  - HTTP API v2 이벤트 파싱 (rawPath, requestContext.http.method, pathParameters)
  - _unzip_source() 추가 (S3 zip → 개별 파일 업로드, CODE_EXT 필터링)
  - Terraform: timeout 30→120s, memory 128→512MB
- B-2b: API Key 인증:
  - authorizer Lambda (x-api-key 헤더 검증)
  - Terraform: random_password, authorizer Lambda + IAM, apigatewayv2_authorizer, route 연결
  - output: api_key (sensitive)
- B-2c: CLI 전환:
  - boto3 제거 → requests 기반
  - _zip_source()로 소스 zip 생성, presigned URL 업로드
  - 환경변수 PROTOCOL_AGENT_URL/KEY 또는 --api-url/--api-key 플래그
- B-2d: 웹 UI 전환:
  - index.html → API Gateway 직접 호출 SPA (API URL/Key localStorage 저장)
  - zip 파일 업로드 → presigned URL → 파이프라인 실행/폴링/결과 다운로드
  - app.py → 단순 정적 파일 서버 (Flask/boto3 완전 제거)

### InProgress
(없음)

## Key Decisions
- Terraform 유지 (CDK 전환 불필요 — IaC 호환성 측면에서 Terraform이 더 좋음)
- API Gateway 경로를 정상화하여 고객 사용 가능한 서비스로 전환 (boto3 직접 호출 → HTTP API)
- API Key 인증: Lambda authorizer 방식 (HTTP API v2 네이티브 API Key 미지원)
- 단계적 접근: Orchestrator 정상화 → 인증 → CLI 전환 → 웹 UI 전환

## Key Analysis
- 기존 CLI/웹 UI가 API Gateway를 사용하지 않고 boto3 직접 호출 — 고객에게 AWS credentials 필요
- Orchestrator가 v1 이벤트 형식 사용 + zip 언팩 미구현 → API Gateway 경로 완전 동작 불가 상태였음
- 목표: 고객은 API Key + curl만으로 사용 가능해야 함 (AWS SDK 불필요)

## Files Modified
- agent-core/lambda/common/prompts/discovery_generator.txt (제외 기준 추가)
- agent-core/lambda/common/prompts/discovery_reviewer.txt (제외 힌트 추가)
- agent-core/lambda/layer_pkg/python/prompts/discovery_generator.txt (동기화)
- agent-core/lambda/layer_pkg/python/prompts/discovery_reviewer.txt (동기화)
- agent-core/lambda/orchestrator/handler.py (전면 수정)
- agent-core/lambda/authorizer/handler.py (신규)
- agent-core/terraform/main.tf (Orchestrator 조정 + authorizer + API Key)
- agent-core/terraform/outputs.tf (api_key output)
- agent-core/client/cli.py (전면 수정: boto3 → requests)
- agent-core/client/web/index.html (전면 수정: API Gateway 직접 호출 SPA)
- agent-core/client/app.py (Flask → 정적 파일 서버)

## Lessons Learned
- HTTP API v2 (apigatewayv2) payload format 2.0은 v1과 이벤트 구조가 다름 (httpMethod→requestContext.http.method, path→rawPath, pathParameters 별도)
- HTTP API v2는 네이티브 API Key를 지원하지 않음 → Lambda authorizer로 구현
- E2E 검증 완료: terraform apply → curl 인증 테스트 → CLI 전체 파이프라인 (53 packets, 5 types, 이전 결과와 동일)

## Context Window Status
~45%

## Next Steps
- C-2: 멀티 에이전트 매니저
