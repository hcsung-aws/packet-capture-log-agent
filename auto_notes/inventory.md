# File Roles

## Source (PacketCaptureAgent/) — 33파일, 5,515줄

### 코어 (캡처/파싱/재현)
| 파일 | 역할 |
|------|------|
| Program.cs (830) | 진입점. CLI 인자 파싱, 모드 분기 (Composition Root) |
| Protocol.cs (236) | JSON 프로토콜 정의 모델 (9개 클래스) |
| PacketParser.cs (302) | TcpStream → 헤더 → 타입 매칭 → 필드 동적 파싱 |
| PacketBuilder.cs (275) | Dictionary → 바이너리 패킷 재구성 |
| PacketReplayer.cs (330) | 로그 파싱 + TCP 재전송 (timing/response/hybrid) |
| PacketFormatter.cs (92) | 파싱 결과 → 콘솔/파일 출력 |
| TcpStream.cs (90) | TCP 스트림 버퍼 재조립, ConnectionKey |
| RawPacketParser.cs (41) | IP/TCP 헤더 파싱 (static) |
| CaptureSession.cs (79) | 캡처 세션 (생성자 주입) |
| ReplayLogger.cs (73) | 리플레이 로그 기록 |
| GameWorldState.cs (65) | 리플레이 중 플레이어/NPC 위치 추적 |

### 분석/시나리오
| 파일 | 역할 |
|------|------|
| SequenceAnalyzer.cs (478) | 캡처 로그 시퀀스 분석 — 분류/Phase/다이어그램/DynamicField |
| ActionCatalogBuilder.cs (311) | Action Catalog 빌드 — 의미 단위 분할 + merge |
| ScenarioBuilder.cs (304) | 시나리오 조립 + DynamicFieldInterceptor + TrackingResponseHandler |
| RecordingStore.cs (89) | 녹화 저장/로드 |
| FieldFlattener.cs (26) | FlattenToState 공통 유틸 |

### BT (Behavior Tree)
| 파일 | 역할 |
|------|------|
| BehaviorTree.cs (134) | BT 노드 모델 + JSON 직렬화 |
| BehaviorTreeBuilder.cs (412) | 녹화 → BT 자동 생성 (explore phase 포함) |
| BehaviorTreeExecutor.cs (126) | BT 런타임 실행 |
| BehaviorTreeEditor.cs (119) | BT CLI 편집기 |
| BehaviorTreeWebEditor.cs (167) | BT 웹 에디터 (HttpListener) |
| ConditionEvaluator.cs (74) | BT 조건 평가 (state expression) |
| ActionExecutor.cs (136) | BT 액션 실행 (패킷 전송/수신) |

### FSM (부하 테스트)
| 파일 | 역할 |
|------|------|
| FsmDefinition.cs (23) | FSM 전이 확률 모델 |
| FsmBuilder.cs (61) | 녹화 → FSM 전이 확률 생성 |
| FsmExecutor.cs (113) | FSM 런타임 실행 |
| LoadTestRunner.cs (77) | 다중 클라이언트 부하 테스트 |

### 인터셉터/변환
| 파일 | 역할 |
|------|------|
| IReplayInterceptor.cs (61) | 리플레이 패킷 가로채기 인터페이스 + ReplaySession |
| NpcAttackInterceptor.cs (50) | NPC 공격 대상 자동 교체 |
| ProximityInterceptor.cs (78) | 근접 NPC 탐색 (FindBestPos 공유) |
| IPacketTransform.cs (46) | 패킷 변환 인터페이스 + TransformFactory |
| RsaDecryptor.cs (134) | RSA 복호화 (Tibia용) |
| XteaDecryptor.cs (83) | XTEA 복호화 (Tibia용) |

## AgentCore (프로토콜 자동 생성)

### Lambda (674줄 Python)
| 파일 | 역할 |
|------|------|
| orchestrator/handler.py (135) | HTTP API v2 라우팅 + zip 언팩 + Step Functions 실행/폴링/결과 |
| authorizer/handler.py (10) | API Key 인증 (x-api-key 헤더 검증) |
| discovery/handler.py (47) | Phase 1: 소스 파일 탐색 + 관련 파일 식별 |
| analysis/handler.py (40) | Phase 2: 패킷 구조 분석 |
| generation/handler.py (44) | Phase 3: JSON 프로토콜 생성 |
| merge/handler.py (34) | Phase 4: 부분 결과 병합 |
| validation/handler.py (92) | Phase 5: 최종 검증 |
| common/llm_client.py (84) | Bedrock LLM 호출 공통 |
| common/s3_helper.py (42) | S3 읽기/쓰기 공통 |
| common/prompt_loader.py (10) | 프롬프트 파일 로더 |

### Client
| 파일 | 역할 |
|------|------|
| cli.py (173) | CLI (requests 기반, API Gateway 호출) |
| app.py (26) | 정적 파일 서버 (웹 UI 호스팅) |
| web/index.html (203) | SPA 웹 UI (API Gateway 직접 호출) |

### Terraform (446줄)
| 파일 | 역할 |
|------|------|
| main.tf (419) | S3, Lambda×7, Layer, Step Functions, API Gateway, Authorizer, API Key |
| outputs.tf (16) | api_url, api_key (sensitive) |
| variables.tf (11) | aws_region, project_name |

## Protocols
| 파일 | 대상 | 패킷 수 |
|------|------|---------|
| echoclient.json | 에코 테스트 | 1 |
| mmorpg_simulator.json | MMORPG 시뮬레이터 | 37 + phases + field_mappings |
| tibia.json | Tibia/ForgottenServer | 5 (RSA+XTEA) |

## Last Updated
2026-04-06, Mickey 21
