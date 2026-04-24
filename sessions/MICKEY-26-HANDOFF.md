# MICKEY-26-HANDOFF

## Current Status
목업 서버 기능 설계 완료 (구현 없음). 방안 A(정적 템플릿) 우선 제안, 사용자 확인 대기.

## Next Steps
목업 서버 구현 — 방안 A(정적 템플릿) 동의 확인 후 진행:
1. MockRule 모델 + MockRuleBuilder (ActionCatalog+recordings → 규칙 자동 생성)
2. MockServer (TCP 리스닝 → C2S 파싱 → 규칙 매칭 → S2C 응답)
3. MockServerMode CLI 진입점 (`--build-mock`, `--mock`)

## Important Context
- 핵심 인사이트: ActionCatalog의 SEND→RECV 시퀀스를 뒤집으면 목업 응답 규칙이 됨
- recordings의 recv_state에서 응답 필드 기본값 추출 가능
- PacketParser(C2S 파싱), PacketBuilder(S2C 조립) 그대로 재사용
- 방안 A(정적 고정 응답) vs B(상태 추적) — A 우선 시작 제안했으나 사용자 미확인
- Tests.csproj TFM 불일치 발견: net10.0 (메인은 net9.0) — CI/CD 시 수정 필요

## Quick Reference
- SESSION: MICKEY-26-SESSION.md
- Context window: 낮음
