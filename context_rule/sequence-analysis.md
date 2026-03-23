# Sequence Analysis Rules

## 요청-응답 매칭 전략
- 이름 패턴 우선: CS_X → SC_X_RESULT (또는 SC_X)
- 이름 패턴 실패 시 위치 기반 폴백: SEND 직후 첫 RECV를 응답으로 간주
- 근거: mmorpg_simulator에서 이름 패턴이 안 맞는 케이스 존재 (Mickey 10)
