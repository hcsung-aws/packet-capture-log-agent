# 암호화 파이프라인 (Transform Pipeline)

패킷 캡처/재현/프록시에서 암호화된 패킷을 처리하기 위한 양방향 Transform 파이프라인입니다.

## 개요

```
수신 (캡처/파싱):  [헤더(평문)] [페이로드(암호화)] → Transform(복호화) → 파싱
송신 (재현/BT/FSM): 패킷 빌드 → ReverseTransform(암호화) → [헤더(평문)] [페이로드(암호화)]
```

- 헤더(size + type)는 항상 평문 유지 — 패킷 경계 식별에 필요
- 페이로드만 암호화/복호화 대상
- Transform 파이프라인은 프로토콜 JSON에서 정의

## 프로토콜 JSON 설정

```json
{
  "protocol": { ... },
  "transforms": [
    {
      "type": "rsa",
      "direction": "C2S",
      "options": {
        "private_key_file": "server.pem",
        "offset": 0,
        "length": 128,
        "xtea_key_output": "xtea_key",
        "use_raw_rsa": true
      }
    },
    {
      "type": "xtea",
      "options": {
        "key": "0123456789ABCDEF0123456789ABCDEF",
        "key_from_context": "xtea_key"
      }
    }
  ],
  "packets": [ ... ]
}
```

### Transform 정의

| 필드 | 설명 | 필수 |
|------|------|------|
| `type` | Transform 종류 (`xtea`, `rsa`) | ✅ |
| `direction` | 적용 방향 (`C2S`, `S2C`, 생략 시 양방향) | |
| `options` | Transform별 옵션 | |

### 방향 (Direction)

| 값 | 의미 |
|----|------|
| `C2S` | 클라이언트→서버 패킷에만 적용 |
| `S2C` | 서버→클라이언트 패킷에만 적용 |
| 생략 | 양방향 모두 적용 |

## 지원 Transform

### XTEA

대칭 블록 암호 (8바이트 블록, 32라운드). 게임 프로토콜에서 널리 사용.

**옵션:**

| 옵션 | 설명 |
|------|------|
| `key` | 16바이트 키 (hex 문자열, 예: `"0123456789ABCDEF0123456789ABCDEF"`) |
| `key_from_context` | TransformContext에서 키를 가져올 이름 (RSA에서 추출된 키 등) |

- `key`와 `key_from_context` 중 하나 이상 지정
- `key_from_context`가 있으면 런타임에 컨텍스트에서 키를 가져옴 (RSA 복호화 후 추출된 세션 키 등)
- 키가 없으면 패스스루 (암호화/복호화 없이 통과)

### RSA

비대칭 암호 (로그인 패킷 등 초기 핸드셰이크용).

**옵션:**

| 옵션 | 설명 |
|------|------|
| `private_key_file` | PEM 형식 개인키 파일 경로 |
| `offset` | RSA 블록 시작 오프셋 (페이로드 내) |
| `length` | RSA 블록 길이 (기본 128 = 1024bit) |
| `xtea_key_output` | 복호화된 데이터에서 XTEA 키를 추출하여 컨텍스트에 저장할 이름 |
| `use_raw_rsa` | Raw RSA 사용 여부 (기본 true, PKCS#1 패딩 없음) |

## 동작 흐름

### 캡처/파싱 (수신)

```
TCP 스트림 → 헤더 읽기(평문) → 패킷 크기 결정 → 전체 패킷 읽기
→ 페이로드 복호화 (Transform 순서대로) → 필드 파싱
```

### 재현/BT/FSM (송신)

```
필드 → PacketBuilder → 헤더 + 페이로드 조립
→ 페이로드 암호화 (ReverseTransform 역순) → 전송
```

### 프록시 모드

```
패스스루: 클라이언트↔서버 raw 중계 + 파싱용 복호화 (상태 동기화)
Takeover: FSM/BT가 PacketBuilder로 패킷 생성 → 암호화 → 서버 전송
          패스스루 중 추출된 세션 키(TransformContext)를 takeover에서 재사용
```

## 커스텀 Transform 추가

`IPacketTransform` 인터페이스를 구현하고 `TransformFactory`에 등록:

```csharp
public class MyTransform : IPacketTransform
{
    public string Name => "MyTransform";
    public byte[] Transform(byte[] data, TransformContext context) { /* 복호화 */ }
    public byte[] ReverseTransform(byte[] data, TransformContext context) { /* 암호화 */ }
}
```

`TransformFactory.Create()`에 새 타입 추가:

```csharp
"mytransform" => new MyTransform(def.Options),
```

## 제한사항

- 헤더는 항상 평문이어야 함 (암호화된 헤더 미지원)
- RSA는 로그인 등 초기 패킷에만 적용 (세션 키 교환 후 XTEA로 전환하는 패턴)
- 암호화 코드(키, 알고리즘)는 대상 게임에서 별도 확보 필요

---

# Encryption Pipeline (Transform Pipeline)

A bidirectional transform pipeline for handling encrypted packets in capture/replay/proxy modes.

## Overview

```
Receive (capture/parse):  [Header(plain)] [Payload(encrypted)] → Transform(decrypt) → Parse
Send (replay/BT/FSM):     Build packet → ReverseTransform(encrypt) → [Header(plain)] [Payload(encrypted)]
```

- Header (size + type) always stays in plaintext — needed for packet boundary detection
- Only payload is encrypted/decrypted
- Transform pipeline is defined in protocol JSON

## Protocol JSON Configuration

```json
{
  "transforms": [
    {
      "type": "rsa",
      "direction": "C2S",
      "options": {
        "private_key_file": "server.pem",
        "offset": 0,
        "length": 128,
        "xtea_key_output": "xtea_key"
      }
    },
    {
      "type": "xtea",
      "options": { "key_from_context": "xtea_key" }
    }
  ]
}
```

### Direction

| Value | Meaning |
|-------|---------|
| `C2S` | Apply only to client→server packets |
| `S2C` | Apply only to server→client packets |
| omitted | Apply to both directions |

## Supported Transforms

### XTEA
Symmetric block cipher (8-byte blocks, 32 rounds). Options: `key` (hex string), `key_from_context` (runtime key from context).

### RSA
Asymmetric cipher for initial handshake. Options: `private_key_file`, `offset`, `length`, `xtea_key_output`, `use_raw_rsa`.

## Adding Custom Transforms

Implement `IPacketTransform` interface (`Transform` for decrypt, `ReverseTransform` for encrypt) and register in `TransformFactory`.

## Limitations

- Header must always be plaintext
- RSA is for initial packets only (session key exchange pattern)
- Encryption keys/algorithms must be obtained from the target game separately
