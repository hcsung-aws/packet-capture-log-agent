# MICKEY-16-SESSION

## Session Meta
- Type: Implementation
- Date: 2026-03-28~30

## Session Goal
BT 자동 생성 품질 향상 (조건 정제 + 상태 바인딩 + 확률 실행 + 상호작용 감지 + 웹 에디터)

## Purpose Alignment
- 기여 시나리오: Phase 3 (시나리오 자동 조립 + 수동 튜닝)
- 이번 세션 범위: BT 자동 생성 → 편집 → 실행 파이프라인 완성

## Previous Context
Mickey 15: 퀘스트/파티 구현 + BT 자동 생성 완료.

## Current Tasks
- [x] A: 조건 자동 정제 — 이중 필터(녹화 내 변화 + 녹화 간 고유값)로 세션 식별자 제거
- [x] mmorpg_simulator: SC_OTHER_CHAR 패킷 분리 (브로드캐스트 노이즈 제거)
- [x] B: 상태 인식 파라미터 바인딩 — {state_random:array}, {state_random:array.field}
- [x] ParseLog 클라이언트 분리 — IP:port 기반 멀티 클라이언트 자동 분리
- [x] 액션 확률(weight) — 녹화 빈도 기반 자동 계산 + 확률 실행
- [x] 상호작용 조건 자동 감지 — SC_OTHER_CHAR 참조 + recv_state 패턴
- [x] --duration 옵션 — 시간 제한 루프 실행
- [x] 웹 에디터 — HttpListener + 바닐라 JS 단일 HTML

## Key Decisions
- 조건 이중 필터: 녹화 내 변화(동적/정적) + 녹화 간 고유값 비율(세션 식별자)
- SC_OTHER_CHAR 분리: 브로드캐스트 노이즈로 인한 recv_state 오염 방지
- state_random 두 패턴: 인덱스(slot) vs 값(itemId) 자동 구분
- weight 스킵은 실패가 아님 → Sequence 계속 진행
- 상호작용 감지: dynamic_field source 기반 + recv_state 패턴 기반 이중 감지
- 웹 에디터: 에이전트 내장 HttpListener, 프론트엔드는 API 호출만

## Progress

### Completed
- A+B: 조건 정제 + 상태 바인딩
- 멀티 클라이언트 캡처 분리
- weight + 상호작용 조건 + duration
- 웹 에디터 (--web-editor)
- E2E 검증 (WSL에서 서버 대상 BT 실행 성공)

## Files Modified
- PacketCaptureAgent: BehaviorTree.cs, BehaviorTreeBuilder.cs, BehaviorTreeExecutor.cs, BehaviorTreeEditor.cs, ActionExecutor.cs, PacketReplayer.cs, Program.cs, PacketCaptureAgent.csproj
- PacketCaptureAgent: BehaviorTreeWebEditor.cs (신규), wwwroot/editor.html (신규)
- protocols/mmorpg_simulator.json (SC_OTHER_CHAR 추가)
- mmorpg_simulator: Protocol.h, GameServer/main.cpp, GameClient/main.cpp

## Lessons Learned
- 브로드캐스트 패킷은 자기 정보 패킷과 반드시 분리 필요
- 녹화 내 값 변화만으로 부족 — 브로드캐스트 노이즈로 세션 고유값도 동적 오분류 가능
- 클라이언트 분리 전 생성된 녹화(mixed)가 남아있으면 조건 정제 오동작
- WSL에서 Windows HttpListener localhost 접근 불가 (Windows 브라우저에서만 접근)

## Context Window Status
~35%

## Next Steps
- C: LLM 수동 검토 기능 (데이터 누적 후)
- 웹 에디터 UI 개선 (필요 시)
