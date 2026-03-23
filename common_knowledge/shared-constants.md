# Shared Constants Pattern

## 규칙
여러 모듈/파일에서 사용하는 상수는 공통 헤더(또는 공유 파일)에 통합 정의한다.

## 안티패턴
각 모듈에서 같은 값을 로컬 정의 → 값 변경 시 불일치 발생.

## 사례
- mmorpg_simulator: MAX_INVENTORY를 GameServer/GameClient에서 각각 정의 → Protocol.h로 통합 (Mickey 11)
