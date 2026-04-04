# Sequence Analysis Rules

## 요청-응답 매칭 전략
- 이름 패턴 우선: CS_X → SC_X_RESULT (또는 SC_X)
- 이름 패턴 실패 시 위치 기반 폴백: SEND 직후 첫 RECV를 응답으로 간주
- 근거: mmorpg_simulator에서 이름 패턴이 안 맞는 케이스 존재 (Mickey 10)

## 동적 필드 감지 규칙
- suffix 타입 필터: *Uid↔*Uid, *Id↔*Id, slot↔slot만 매칭 (값 기반만으로는 ID 겹침 오탐)
- 시간 순서 필수: SEND 이전 RECV만 후보 (미준수 시 결과 패킷이 소스로 잡히는 근본적 오류)
- 수동 오버라이드: 프로토콜 JSON field_mappings (external/static) 우선 적용
- 근거: Mickey 12에서 시간 순서 미준수로 4건 오탐 발생, suffix 필터로 해결
