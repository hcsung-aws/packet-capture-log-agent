# MICKEY-16-HANDOFF

## Current Status
BT 자동 생성 품질 대폭 향상 + 웹 에디터 추가. 조건 정제(A) + 상태 바인딩(B) + weight + 상호작용 감지 + duration + 웹 에디터 완료. E2E 검증 통과.

## Next Steps
1. C: LLM 수동 검토 기능 (데이터 누적 후)
2. 웹 에디터 UI 개선 (필요 시)

## Important Context
- 클라이언트 분리 전 생성된 녹화(mixed)가 남아있으면 조건 정제 오동작 — 반드시 제거 후 rebuild
- WSL에서 --behavior는 동작하지만 --web-editor는 Windows에서만 브라우저 접근 가능
- weight 스킵은 Sequence 실패가 아님 (true 반환)

## Quick Reference
- SESSION: MICKEY-16-SESSION.md
- 웹 에디터: `--web-editor bt.json [--web-port 8080]`
- BT 실행: `--behavior bt.json -t host:port [--duration 60]`
- 테스트: `dotnet test PacketCaptureAgent.Tests --no-restore -v quiet`
- Context window: ~35%
