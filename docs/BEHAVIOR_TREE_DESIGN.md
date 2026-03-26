# Behavior Tree 설계 문서

## 개요

녹화된 플레이 세션들로부터 Behavior Tree를 자동 구축하고, 조건 기반 분기로 다이나믹한 시나리오 재현을 수행하는 시스템.

## 아키텍처

```
┌──────────────────────────────────────────────┐
│            Behavior Tree Layer (신규)          │
│                                               │
│  RecordingStore      — 녹화 세션 저장/인덱싱     │
│  BehaviorTreeBuilder — 녹화 → 트리 자동 구축     │
│  BehaviorTree        — 트리 모델 (JSON 직렬화)   │
│  ConditionEvaluator  — 분기 조건 평가            │
│  BehaviorTreeEditor  — 인터랙티브 편집           │
│  BehaviorTreeExecutor— 런타임 트리 순회+실행     │
│                                               │
├───────────── 연결 지점 ──────────────────────┤
│  ActionExecutor (신규) — 단일 액션 실행          │
│    └─ PacketBuilder, IResponseHandler,        │
│       IReplayInterceptor 재사용               │
│                                               │
├──────────────────────────────────────────────┤
│          기존 Infrastructure (변경 없음)        │
│                                               │
│  ActionCatalog / ScenarioBuilder / Replayer   │
│  DynamicFieldInterceptor / NpcAttackInterceptor│
│  ReplayContext (SessionState, World)          │
└──────────────────────────────────────────────┘
```

## 노드 모델

| 노드 | 역할 | 속성 |
|------|------|------|
| Action | 리프. ActionCatalog 액션 실행 | actionId, overrides? |
| Sequence | 자식을 순서대로 실행 | children[] |
| Selector | 조건 맞는 첫 번째 자식 실행 | children[] |
| Condition | SessionState 기반 분기 판단 | expression |
| Repeat | 자식 반복 실행 | count, child |

## 조건 시스템 (ConditionEvaluator)

- 평가 대상: `ReplayContext.SessionState` (flat key)
- 표현식: `SC_CHAR_LIST.count == 0`, `SC_CHAR_LIST.count > 0`
- 복합: `AND(cond1, cond2)`, `OR(cond1, cond2)`
- 폴백: LLM 조건 (표현식 불가 시)

## ActionExecutor — 기존 로직 재사용

단일 액션 실행 프리미티브. 기존 PacketReplayer는 수정하지 않음.

```
ActionExecutor.Execute(action, session)
  → PacketBuilder로 SEND 패킷 빌드
  → 인터셉터 체이닝 (Priority 시스템)
  → 전송 + 응답 대기 (IResponseHandler)
  → SessionState 업데이트 (TrackingResponseHandler)
  → 결과 반환
```

## RecordingStore 포맷

```json
{
  "recordings": [
    {
      "id": "rec_001",
      "timestamp": "2026-03-26T...",
      "actions": ["login", "char_create", "char_select", "move", "attack"],
      "transitions": [
        {"from": "login", "to": "char_create", "condition": "SC_CHAR_LIST.count == 0"},
        {"from": "login", "to": "char_select", "condition": "SC_CHAR_LIST.count > 0"}
      ]
    }
  ]
}
```

## 트리 JSON 포맷

```json
{
  "name": "full_gameplay",
  "root": {
    "type": "sequence",
    "children": [
      {"type": "action", "id": "login"},
      {
        "type": "selector",
        "children": [
          {
            "type": "sequence",
            "condition": "SC_CHAR_LIST.count == 0",
            "children": [
              {"type": "action", "id": "char_create"},
              {"type": "action", "id": "char_select"}
            ]
          },
          {"type": "action", "id": "char_select", "condition": "SC_CHAR_LIST.count > 0"}
        ]
      }
    ]
  }
}
```

## 구현 단계

| 단계 | 내용 | 의존 | 산출물 |
|------|------|------|--------|
| A. 모델 | BT 노드 모델 + JSON 직렬화 + ConditionEvaluator | 없음 | BehaviorTree.cs, ConditionEvaluator.cs |
| B. 실행 | ActionExecutor + BT Executor + CLI | A | ActionExecutor.cs, BehaviorTreeExecutor.cs |
| C. 녹화 | RecordingStore + 전환 조건 자동 추출 | A | RecordingStore.cs |
| D. 구축 | 녹화 → 트리 자동 생성 | A, C | BehaviorTreeBuilder.cs |
| E. 편집 | 인터랙티브 CLI 편집 | A | BehaviorTreeEditor.cs |
| F. LLM | LLM 기반 조건 분기 폴백 | A, B | ConditionEvaluator 확장 |

## 기존 코드 영향

- 변경 없음: PacketReplayer, ScenarioBuilder, ActionCatalog, 인터셉터
- 재사용: PacketBuilder, IResponseHandler, IReplayInterceptor, ReplayContext
- 신규 파일만 추가

## 설계 원칙

1. Action 노드 = ActionCatalog 액션 (기존 단위 재사용)
2. 기존 시나리오 기능과 완전 분리 (새 파일, 새 CLI 옵션)
3. 조건 평가는 SessionState flat key 기반 (이미 구현된 인프라)
4. 점진적 구현: A→B로 수동 트리 실행 가능, C→D로 자동 구축 추가

## Last Updated
2026-03-26, Mickey 14
