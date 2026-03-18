# Phase 3 계획 (보류)

## 상태
보류 — mmorpg_simulator 기능 보강 후 진행 예정

## 선행 작업 (packet-capture-log-agent)
1. PacketFormatter 배열 출력 수정 (구조체 배열 필드 풀어서 출력)
2. 노이즈 분류 세분화 (replay_skip vs data_source)

## 프로토타이핑 결과
- `prototype/analysis_result.json` — 패킷 분류 + 동적 필드 분석
- `prototype/analyze_scenario.py` — 분석 스크립트
- `prototype/other_player_packet_analysis.md` — 다른 플레이어 패킷 필터링 분석
- `prototype/testplay_design.md` — Behavior Tree 기반 TestPlay 설계

## 핵심 설계 결정
- 단순 Replay(회귀 테스트)와 TestPlay(BT 기반 부하/시나리오 테스트)를 별도 모듈로 분리
- Core(Protocol, PacketParser, PacketBuilder)는 공유
- 구현 순서: BT 기본 → GameState → Actions → 시나리오 검증 → 다중 클라이언트

## 관련 세션
- Mickey 5: 프로토타이핑 + 설계
