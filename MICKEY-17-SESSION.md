# MICKEY-17-SESSION

## Session Meta
- Type: Implementation
- Date: 2026-03-30

## Session Goal
PacketCaptureAgent 범용화 — 다양한 게임 대응을 위한 하드코딩 제거 + 타입 시스템 확장 + 프로토콜 자동 생성

## Purpose Alignment
- 기여 시나리오: Phase 1 (패킷 구조 분석 → JSON 프로토콜 생성) + Phase 3 (시나리오 자동 조립)
- 이번 세션 범위: BT Builder 범용화 → 타입 확장 → 프로토콜 자동 생성 기반

## Previous Context
Mickey 16: BT 자동 생성 품질 향상 완료 (조건 정제 + 상태 바인딩 + weight + 상호작용 감지 + duration + 웹 에디터). E2E 검증 통과.

## Current Tasks
- [x] 1: BT Builder 범용화 — 하드코딩된 게임 특화 로직을 protocol JSON semantics로 추출
- [x] 2: 타입 시스템 확장 — length-prefixed 문자열, 조건부 필드
- [x] 3: 프로토콜 JSON 자동 생성 PoC — Bedrock 멀티 에이전트 기반

## Progress

### Completed
- 1+2: 이전 세션에서 완료 (semantics, string_prefixed, conditional)
- 3: PoC 전체 파이프라인 구현 + E2E 검증 완료
  - 5 Phase: Discovery → Analysis → Merge → Generation → Validation
  - Generator + Reviewer 2-agent 상호 검증 패턴
  - mmorpg_simulator 소스 → 53 packets + 5 types 자동 생성
  - 참조 JSON 대비: 공통 51개 패킷 모든 필드 완벽 일치
  - 소요 시간: ~8분 (Sonnet 4)

## Key Decisions
- AgentCore는 AWS 클라우드 (Lambda + Step Functions), 로컬은 HTTP 클라이언트만 → NuGet 의존성 불필요
- 멀티 에이전트 상호 검증: Generator + Reviewer 2-agent 패턴 (Phase별)
- 단계별 독립 실행 + 파이프라인 래핑 (디버깅/유지보수 원칙)
- LLM Provider: Bedrock (inference profile 필요: us.anthropic.claude-sonnet-4-20250514-v1:0)
- 인프라: Terraform
- Lambda: Python
- PoC 먼저 → 검증 후 AgentCore 구현

## Files Modified
- agent-core/poc/: config.py, llm_client.py, discovery.py, analysis.py, merge.py, generation.py, validation.py, pipeline.py
- agent-core/poc/prompts/: 6개 프롬프트 템플릿
- agent-core/poc/requirements.txt
- agent-core/lambda/: common/(llm_client, s3_helper, prompt_loader, prompts/), discovery/, analysis/, merge/, generation/, validation/, orchestrator/
- agent-core/lambda/layer_pkg/: Lambda Layer 패키지 (python/ prefix)
- agent-core/terraform/: main.tf, variables.tf, outputs.tf
- agent-core/client/: cli.py, app.py, web/index.html, requirements.txt

## Lessons Learned
- Bedrock on-demand는 inference profile ID 필요 (모델 ID 직접 사용 불가)
- LLM JSON 응답에서 markdown fence + 추가 텍스트 포함 가능 → raw_decode로 첫 JSON 객체 추출 필요
- Reviewer가 list 반환하는 경우 있음 → dict 타입 체크 필수
- Protocol.h 분석 시 max_tokens 8192 부족 → 16384 필요 (50+ structs)
- read_timeout 60초 기본값 부족 → 300초로 증가 필요
- generate_protocol.py 같은 파서 스크립트는 Analysis에서 구조체 추출 불가 → Discovery에서 role 필터링 개선 여지
- 세션 로그/핸드오프에 설계 논의 상세 내용 포함 필요 (이전 세션에서 누락됨)
- Lambda Layer는 python/ prefix 필수 (source_dir 구조 주의)
- Step Functions에서 Lambda 반환값으로 다음 단계 입력 구성 시, 필요한 데이터를 Lambda가 직접 반환해야 함 (S3에만 저장하면 SFN에서 접근 불가)
- API Gateway HTTP API는 CORS 설정이 간단 (REST API보다 권장)

## Context Window Status
~40%

## Next Steps
- 모델 비교 PoC (Haiku, Sonnet 3.7 vs Sonnet 4 — 비용 대비 효율)
- Discovery role 필터링 개선 (파서 스크립트 제외)
- API Gateway 인증 추가 (IAM 또는 API Key)
- 웹 UI에서 중간 결과(metadata.json 등) 조회 기능
