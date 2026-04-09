# MICKEY-22-HANDOFF

## Current Status
프로토콜 자동 생성 에이전트 정확도 개선 완료. v0(type 0/51) → v7(type 51/51, 필드 0 차이). Generation 결정론화 + handler 분석 추가.

## Next Steps
Discovery에서 missing_dependencies 감지 시 CLI 경고 표시 개선. E2E 실제 실행 검증 (에이전트+매니저).

## Important Context
- 소스 경로는 프로젝트 루트 필수 (GameServer/만 지정하면 Common/ 누락 → 전부 오류)
- Generation은 결정론적 코드, semantics만 LLM (하이브리드)
- CLI 인자 순서: --api-url/--api-key는 서브커맨드(generate) 앞에

## Quick Reference
- SESSION: MICKEY-22-SESSION.md
- auto_notes: NOTES.md (9파일)
- Context window: 높음
