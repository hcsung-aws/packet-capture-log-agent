# MICKEY-26-SESSION

## Session Meta
- Type: Planning
- Mickey: 26
- Date: 2026-04-13 ~ 04-14

## Session Goal
다음 작업 우선순위 결정 + 목업 서버 기능 설계

## Purpose Alignment
- 기여 시나리오: QA 자동화 확장 — 실제 서버 없이 클라이언트 테스트 가능한 목업 서버
- 이번 세션 범위: 분석 + 설계만 (구현 없음)

## Previous Context
- MICKEY-25: 프록시 모드 E2E 검증 완료 (FSM takeover 정상, BT 차단). 테스트 212개 통과.

## Current Tasks

### 1. 다음 작업 우선순위 결정 ✅
- CI/CD vs 프록시 HTTP API 비교 → CI/CD 우선 제안
- 사용자가 CI/CD 보류, 목업 서버 기능 요청

### 2. 목업 서버 기능 설계 ✅
- 현재 구현 분석: ProxyServer, PacketParser, PacketBuilder, ActionCatalog, recordings 구조 파악
- 재사용 자산 식별: PacketParser(C2S 파싱), PacketBuilder(S2C 조립), ActionCatalog(요청→응답 매핑)
- 응답 규칙 전략: ActionCatalog의 SEND→RECV 시퀀스를 뒤집어 C2S→S2C 매핑 규칙 생성
- 방안 A(정적 템플릿) vs B(상태 추적) 비교 → A 우선 시작, 점진 확장 제안
- 사용자 확인 대기 중 (방안 A 동의 여부)

## Progress
- Completed: 분석 + 설계
- InProgress: 없음
- Blocked: 사용자 확인 (방안 A/B 선택)

## Key Decisions
1. **CI/CD 보류**: 사용자 요청으로 목업 서버 기능 우선
2. **테스트 프로젝트 TFM 불일치 발견**: Tests.csproj가 net10.0, 메인이 net9.0 — CI/CD 진행 시 수정 필요

## Files Modified
- 없음 (분석/설계만)

## Lessons Learned
- ActionCatalog의 SEND→RECV 시퀀스가 목업 서버 응답 규칙의 핵심 소스로 재사용 가능

## Context Window Status
낮음

## Next Steps

### 목업 서버 구현 (방안 A: 정적 템플릿 우선)

구현 순서:
1. **MockRule 모델**: C2S 패킷명 → 응답 S2C 패킷 목록 + 필드 템플릿
2. **MockRuleBuilder**: ActionCatalog + recordings → MockRule 자동 생성 (`--build-mock`)
3. **MockServer**: TCP 리스닝 → C2S 파싱 → MockRule 매칭 → S2C 조립/전송
4. **MockServerMode**: CLI 진입점 (`--mock`)

설계 상세:
- 응답 규칙: ActionCatalog의 SEND→RECV 시퀀스를 뒤집어 자동 생성
- 응답 필드 값: recordings의 recv_state에서 실제 값 추출 (기본 템플릿)
- 재사용: PacketParser(C2S 파싱), PacketBuilder(S2C 조립) 그대로 사용
- CLI: `--build-mock` (규칙 생성), `--mock rules.json --port 9000` (서버 실행)

사용자 확인 필요:
- 방안 A(정적 템플릿) 우선 진행 동의 여부
