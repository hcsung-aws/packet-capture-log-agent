# LLM Pipeline Design — Deterministic Hybrid Pattern

## 원칙
LLM 파이프라인에서 결정론적 변환이 가능한 단계는 코드로 전환한다.

## 적용 기준
- **LLM 필요**: 자연어 이해, 의미 추론, 코드 분석 (Discovery, Analysis, Merge)
- **코드로 전환**: 형식 변환, 타입 매핑, 스키마 변환 (Generation)
- **하이브리드**: 구조는 코드, 의미 추론만 LLM (예: packets는 코드, semantics는 LLM)

## 효과
- 재현성: 동일 입력 → 동일 출력 (LLM 비결정성 제거)
- 정확도: 규칙 기반 변환은 100% 정확 (count_field, type mapping 등)
- 속도: LLM 호출 제거 → 수초 이내
- 디버깅: 중간 결과(metadata)와 최종 결과의 차이를 코드로 추적 가능

## 안티패턴
- 프롬프트만으로 LLM 비결정성 해결 시도 → 개선 한계 있음
- 전체 파이프라인을 LLM에 의존 → 단계별 검증 불가

## 출처
Mickey 22 — agent-core Generation 결정론화 (v0 type 0/51 → v7 type 51/51, 필드 0 차이)
