# MICKEY-25-SESSION (continued)

## 추가 작업 (2026-04-11 ~ 04-13)

### 7. PURPOSE-SCENARIO + ShowUsage + README 문서 보강 ✅
- PURPOSE-SCENARIO: Phase 4 완료 표시, Phase 5 프록시 시나리오 추가
- ShowUsage: 프록시 옵션 섹션 추가
- README: 한국어/영문 모두 프록시 모드 + BT/FSM/멀티에이전트 보강
- git push f265284

### 8. 프록시 E2E 테스트 — FSM ✅
- mmorpg-simulator 서버 + GameClient + 프록시 연동 검증
- 패스스루 정상, takeover → FSM 실행 정상
- 발견 이슈 3건 수정:
  - FsmExecutor: proxy 모드에서 connect/disconnect 시 FSM 종료 (새 TCP 연결 방지)
  - ForwardingResponseHandler: takeover 중 서버 응답을 클라이언트에도 전달 (NPC_DEATH 반영)
  - RelayAsync: takeover 중 서버→클라이언트 relay 일시중단 (NetworkStream 경합 방지)
- git push 7dad9ef

### 9. 프록시 E2E 테스트 — BT ✅ (동작하지만 설계 부적합)
- BT takeover 자체는 동작 (15 액션 실행, passthrough 복귀)
- 문제: BT는 "처음부터 순서대로 실행" 전제 → 중간 진입 시 이미 완료된 액션 재실행
  - char_create → Create failed, quest_complete → not completable 등
- 결론: BT는 Build Validation(처음부터) 용도, 프록시 takeover에는 FSM이 적합
- ProxyMode에서 BT 차단 + 에러 핸들링 추가
- git push 7dcface

### 10. 코드 정리 ✅
- LoadTestRunner.Run 동기 래퍼 제거
- ScenarioBuilderTests xUnit1031 경고 수정 (async/await)
- BtSyncHandler: TrackingResponseHandler → IResponseHandler

### 11. GameClient 포트 파라미터화 (mmorpg_simulator)
- 서버 포트 입력 추가 (기본값 9000 유지)

## Lessons Learned
1. 프록시 takeover에서 같은 NetworkStream을 FSM과 RelayAsync가 동시에 읽으면 경합 발생 → takeover 중 relay 일시중단 필수
2. BT는 순차 실행 전제라 중간 진입에 부적합, FSM은 확률 기반이라 현재 상태에서 자연스럽게 동작
3. [Protocol] Task.Run 내부의 async 예외는 외부에서 관찰되지 않을 수 있음 → try-catch 필수

## Next Steps
- CI/CD 파이프라인 (GitHub Actions)
- 프록시 HTTP API (외부 스크립트 제어)
