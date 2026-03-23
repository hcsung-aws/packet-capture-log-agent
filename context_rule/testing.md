# 테스트 지침

## 필수 규칙

1. **수정 전 테스트 실행**: 코드 변경 전 `dotnet test` 통과 확인
2. **수정 후 테스트 실행**: 변경 후 전체 테스트 통과 확인
3. **새 기능 = 새 테스트**: 기능 추가 시 해당 컴포넌트 테스트도 추가
4. **버그 수정 = characterization test 먼저**: 버그 수정 전 현재 동작을 캡처하는 테스트 작성 → 수정 → 테스트 갱신
5. **소스 수정 후 리빌드 필수**: 코드 변경 후 반드시 `dotnet build` → `dotnet test` 순서. 리빌드 없이 테스트 실행은 이전 바이너리를 검증하는 것 (Mickey 8에서 반복 발생)

## 테스트 실행

```bash
# WSL2에서 실행
cd PacketCaptureAgent.Tests
"/mnt/c/Program Files/dotnet/dotnet.exe" test --no-restore -v quiet

# 특정 클래스만
"/mnt/c/Program Files/dotnet/dotnet.exe" test --filter "PacketParserTests" --no-restore -v quiet
```

## 테스트 파일 구조

| 파일 | 대상 | 테스트 수 |
|------|------|----------|
| TcpStreamTests.cs | TcpStream (버퍼 관리) | 8 |
| PacketParserTests.cs | PacketParser (바이너리→필드) | 12 |
| PacketBuilderTests.cs | PacketBuilder (필드→바이너리) | 10 |
| GameWorldStateTests.cs | GameWorldState (상태 추적) | 12 |
| PacketReplayerParseLogTests.cs | PacketReplayer.ParseLog (로그 파싱) | 8 |
| PacketFormatterTests.cs | PacketFormatter (출력 포맷) | 9 |

| CliParseArgsTests.cs | Program.ParseArgs (CLI 인자 파싱) | 10 |

## 테스트 불가 컴포넌트 (네트워크/OS 의존)

- Program.cs (Raw Socket, 인터랙티브 입력)
- PacketReplayer.Replay (TCP 연결)
- NpcAttackInterceptor (ReplaySession 네트워크)
- RsaDecryptor / XteaDecryptor (현재 미사용, 향후 필요 시 추가)

## Last Updated
2026-03-21, Mickey 9
