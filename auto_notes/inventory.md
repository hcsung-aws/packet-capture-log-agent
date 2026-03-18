# File Roles

## Source (PacketCaptureAgent/)
| 파일 | 역할 |
|------|------|
| Program.cs | 진입점. CLI 인자 파싱, Raw Socket 캡처 루프, 캡처/재현 모드 분기 |
| Protocol.cs | JSON 프로토콜 정의 모델 (ProtocolDefinition, HeaderInfo, PacketDefinition 등 9개 클래스) |
| PacketParser.cs | TcpStream에서 헤더 읽기 → 패킷 타입 매칭 → 필드별 동적 파싱 |
| PacketBuilder.cs | 파싱된 Dictionary → 바이너리 패킷 재구성 (재현용) |
| PacketReplayer.cs | 로그 파일 파싱 + TCP 소켓으로 재전송 (timing/response/hybrid) + 인터셉터 지원 |
| PacketFormatter.cs | 파싱 결과 → 콘솔/파일 출력 포맷 (컬러 콘솔 + 플레인 파일) |
| TcpStream.cs | TCP 스트림 버퍼 재조립, ConnectionKey 레코드, TcpStreamManager |
| GameWorldState.cs | 리플레이 중 플레이어/NPC 위치 추적 (서버 응답 기반) |
| IReplayInterceptor.cs | 리플레이 패킷 가로채기 인터페이스 + ReplaySession 헬퍼 |
| NpcAttackInterceptor.cs | CS_ATTACK 감지 → NPC 탐색 → 이동 → targetUid 교체 |
| IPacketTransform.cs | 패킷 변환 인터페이스 + TransformContext + TransformFactory |
| RsaDecryptor.cs | RSA 복호화 (Tibia 프로토콜용) |
| XteaDecryptor.cs | XTEA 복호화 (Tibia 프로토콜용) |

## Protocols (protocols/)
| 파일 | 대상 | 패킷 수 |
|------|------|---------|
| echoclient.json | 에코 테스트 | 1 |
| mmorpg_simulator.json | MMORPG 시뮬레이터 | 36 (로그인/캐릭터/이동/전투/출석/채팅/아이템/상점/하트비트) |
| tibia.json | Tibia/ForgottenServer | 5 (RSA+XTEA 암호화) |
