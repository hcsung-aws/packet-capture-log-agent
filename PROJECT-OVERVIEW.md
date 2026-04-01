# PROJECT-OVERVIEW

## Project Name
Packet Capture Log Agent

## Goal
TCP 패킷 캡처 → JSON 프로토콜 파싱 → 로그 기록 → 패킷 재현 → QA 자동화 도구

## Scope
- TCP 패킷 캡처 (Windows Raw Socket)
- JSON 기반 동적 프로토콜 파싱 (정수, 문자열, 배열, 구조체, length-prefixed 문자열, 조건부 필드)
- 패킷 변환 파이프라인 (RSA, XTEA 복호화)
- 캡처 로그 기반 패킷 재현 (timing/response/hybrid 모드)
- Behavior Tree 자동 생성 + 확률 실행 + 웹 에디터
- 프로토콜 자동 생성 (LLM 멀티 에이전트 — AgentCore)
- 다중 클라이언트 부하 테스트 (Phase 4 Step 1 완료)

## Constraints
- Windows 전용 (Raw Socket), 127.0.0.1 loopback 캡처 불가
- TCP만 지원 (UDP 미지원)
- 암호화 패킷은 transform 파이프라인 필요
- 관리자 권한 필요 (Raw Socket)

## Success Criteria
- mmorpg-simulator 대상 캡처→파싱→재현 파이프라인 정상 동작
- 시나리오 기반 다중 클라이언트 동시 재현
- 새로운 게임 소스 → 프로토콜 JSON 자동 생성 (수동 작업 최소)

## Current Status
- E2E 파이프라인 완료 (캡처→파싱→재현→BT 자동 생성→실행)
- AgentCore 배포 완료 (AWS Lambda + Step Functions + API Gateway)
- CLI + 웹 프론트엔드 완료

## Last Updated
2026-04-01
