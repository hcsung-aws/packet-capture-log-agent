# Proxy Design Rules

## NetworkStream 경합
- takeover 중 relay(서버→클라이언트 전달) 일시중단 필수
- 같은 NetworkStream을 FSM/BT와 RelayAsync가 동시에 읽으면 경합 발생
- ForwardingResponseHandler로 서버 응답을 클라이언트에도 전달

## Takeover 모드 제약
- FSM만 프록시 takeover 지원 (확률 기반, 현재 상태에서 자연스럽게 동작)
- BT는 프록시 takeover 부적합 — 순차 실행 전제, 중간 진입 시 완료 액션 재실행
- ProxyMode에서 BT 차단 처리 완료

## Last Updated
2026-04-17
