# MICKEY-15-HANDOFF

## Current Status
퀘스트(0x0Axx) + 파티(0x0Bxx) 시스템 구현 완료. BT 자동 생성 완료 (7건 녹화 → mmorpg_simulator_auto.json). 다음: BT 조건 편집 + E2E 검증.

## Next Steps
1. BT 조건 수동 편집 (--edit-behavior): accountUid 기반 → 게임 상태 기반 조건
2. BT 실행 E2E 검증 (--behavior 옵션으로 서버 대상 실행)

## Important Context
- 버그 수정 3건: Broadcast loggedIn 체크(SessionManager.cpp), OnMove name 포함, ShopBuy 실패 시 gold 반환
- 멀티 클라이언트 캡처: 127.0.0.1(loopback, 캡처 안됨) + 실제 IP(캡처됨) 분리 전략 확인
- 리플레이/BT는 현재 단일 클라이언트만 지원 (ParseLog에서 IP:port 무시, 단일 TCP 연결)
- BT 단계 F(LLM 조건 폴백) 미구현 — 필요 시 추가
- mmorpg_simulator 프로토콜: 총 50개 패킷 (Auth~Party)

## Quick Reference
- SESSION: MICKEY-15-SESSION.md
- BT 파일: behaviors/mmorpg_simulator_auto.json
- 녹화: recordings/mmorpg_simulator_recordings.json (7건)
- 테스트: `"/mnt/c/Program Files/dotnet/dotnet.exe" test PacketCaptureAgent.Tests --no-restore -v quiet`
- Context window: ~25%
