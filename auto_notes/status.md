# Current Status & Gap Analysis

## 동작 확인됨 (기존 로그 기반)
- mmorpg-simulator 대상 캡처 + 파싱 동작 (2026-01-28 로그)
- CS_LOGIN → SC_LOGIN_RESULT → SC_CHAR_LIST 시퀀스 정상 파싱
- 필드 값 + raw hex 로그 모두 정상 기록

## 기능별 상태

| 기능 | 상태 | 상세 |
|------|------|------|
| TCP 캡처 | ✅ | Raw Socket, 포트 필터, 자동 로그 파일 |
| 프로토콜 파싱 | ✅ | 기본타입 + array + struct + enum |
| 로그 생성 | ✅ | 텍스트 형식 (콘솔 + 파일) |
| 단일 재현 | ✅ | timing/response/hybrid, 응답 파싱 |
| Transform | ✅ | RSA + XTEA 파이프라인 |
| 시퀀스 분석 | ✅ | Core/DataSource/Conditional/Noise 분류 + ASCII/Mermaid + Phase 구분 |
| Dynamic Field 감지 | ✅ | suffix 타입 필터 + 시간 순서 + 수동 오버라이드 (field_mappings) |
| Action Catalog | ✅ | 의미 단위 분할 + merge 저장 (actions/{protocol}_actions.json) |
| 시나리오 조립 | ✅ | ScenarioBuilder — 카탈로그에서 Action 선택 → 시나리오 JSON 생성 → 재현 (E2E 미검증) |
| 다중 클라이언트 | ❌ | 단일 TCP 연결만 |
| 부하/회귀 테스트 | ❌ | 미구현 |
| Kiro context | ❌ | 미구현 |

## 목표까지의 단계
1. E2E 검증 (캡처→재현 파이프라인 실제 동작 확인)
2. 시나리오 자동 조립 (의미론적 패킷 시퀀스 분석)
3. 다중 클라이언트 동시 재현
4. 부하/회귀 테스트 프레임워크
