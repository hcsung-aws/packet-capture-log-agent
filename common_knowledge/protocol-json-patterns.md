# Protocol JSON Patterns

게임 프로토콜 JSON 작성 시 참고하는 범용 패턴.
상세 스키마: docs/PROTOCOL_SCHEMA.md

## 헤더 패턴
- Pattern A: uint16 length + uint16 type (4B) — 가장 일반적
- Pattern B: uint32 length + uint32 type (8B)
- Pattern C: type first, length second
- Pattern D: uint8 type + uint16 length (3B)

## 필드 타입
- 기본: int8~64, uint8~64, float, double, bool
- 가변: string (length 필수), bytes (length 필수)
- 고급: array (element + count_field 또는 length), struct (types에 정의), enum (base 타입)

## 배열 정의 주의사항
- `count_field`: 다른 필드 값을 동적 카운트로 사용 (권장)
- `length`: 고정 개수 — 실제 데이터와 불일치 가능성 있음
- 예: `{"name": "items", "type": "array", "count_field": "itemCount", "element": "ItemEntry"}`

## 새 프로토콜 작성 체크리스트
1. endian 확인 (대부분 little)
2. pack 확인 (소스의 `#pragma pack`)
3. 헤더 구조 (length/type 필드 타입, 순서, 오프셋)
4. 패킷별 필드 정의 (구조체와 일치 확인)
5. 배열은 count_field 사용 권장
6. 커스텀 타입은 types에 struct/enum 정의
