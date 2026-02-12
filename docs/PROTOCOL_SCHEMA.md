# Protocol JSON Schema Guide

## Overview

프로토콜 JSON 파일은 패킷 구조를 정의합니다. 이 가이드는 다양한 게임 프로토콜에 대응하기 위한 설정 방법을 설명합니다.

## Basic Structure

```json
{
  "protocol": {
    "name": "Game Name",
    "version": "1.0",
    "endian": "little",
    "pack": 1,
    "header": { ... }
  },
  "packets": [ ... ]
}
```

## Protocol Options

### endian
- `"little"` (default): Little-endian (Intel, most games)
- `"big"`: Big-endian (network byte order, some older games)

### pack
구조체 패킹 설정. C/C++의 `#pragma pack(push, N)`에 해당.

| Value | Description | Use Case |
|-------|-------------|----------|
| `1` | No padding (default) | `#pragma pack(push, 1)` 사용하는 게임 |
| `4` | 4-byte alignment | 32-bit 기본 정렬 |
| `8` | 8-byte alignment | 64-bit 기본 정렬 |

**Example**: pack이 8이고 `uint8` 다음에 `uint64`가 오면 7바이트 패딩 필요.

## Header Configuration

### Option 1: Explicit Fields (Recommended)

```json
"header": {
  "size_field": "length",
  "type_field": "type",
  "fields": [
    { "name": "length", "type": "uint16", "offset": 0 },
    { "name": "type", "type": "uint16", "offset": 2 }
  ]
}
```

헤더 크기는 `max(offset + type_size)`로 자동 계산됩니다.
위 예시: offset 2 + uint16(2) = 4 bytes

### Option 2: Explicit Size

```json
"header": {
  "size_field": "length",
  "type_field": "type",
  "size": 8,
  "fields": [
    { "name": "length", "type": "uint32", "offset": 0 },
    { "name": "type", "type": "uint32", "offset": 4 }
  ]
}
```

### Common Header Patterns

**Pattern A: uint16 length + uint16 type (4 bytes)**
```json
"fields": [
  { "name": "length", "type": "uint16", "offset": 0 },
  { "name": "type", "type": "uint16", "offset": 2 }
]
```

**Pattern B: uint32 length + uint32 type (8 bytes)**
```json
"fields": [
  { "name": "length", "type": "uint32", "offset": 0 },
  { "name": "type", "type": "uint32", "offset": 4 }
]
```

**Pattern C: uint16 type + uint16 length (type first)**
```json
"size_field": "length",
"type_field": "type",
"fields": [
  { "name": "type", "type": "uint16", "offset": 0 },
  { "name": "length", "type": "uint16", "offset": 2 }
]
```

**Pattern D: uint8 type + uint16 length (3 bytes)**
```json
"fields": [
  { "name": "type", "type": "uint8", "offset": 0 },
  { "name": "length", "type": "uint16", "offset": 1 }
]
```

## Field Types

| Type | Size | Description |
|------|------|-------------|
| `int8` / `uint8` | 1 | Signed/unsigned byte |
| `int16` / `uint16` | 2 | Signed/unsigned short |
| `int32` / `uint32` | 4 | Signed/unsigned int |
| `int64` / `uint64` | 8 | Signed/unsigned long |
| `float` | 4 | 32-bit float |
| `double` | 8 | 64-bit double |
| `bool` | 1 | Boolean (0/1) |
| `string` | N | Fixed-length string (requires `length`) |
| `bytes` | N | Raw bytes (requires `length`) |

## Packet Definition

```json
{
  "type": 257,
  "name": "CS_LOGIN",
  "direction": "C2S",
  "fields": [
    { "name": "accountId", "type": "string", "length": 32 },
    { "name": "password", "type": "string", "length": 32 }
  ]
}
```

### Direction
- `"C2S"`: Client to Server
- `"S2C"`: Server to Client

## Handling Struct Padding

### When pack = 1 (No Padding)
필드를 순서대로 정의하면 됩니다.

### When pack > 1 (With Padding)
패딩이 필요한 위치에 `bytes` 타입 필드를 추가합니다.

**Example**: pack=8, uint8 다음에 uint64
```json
"fields": [
  { "name": "success", "type": "uint8" },
  { "name": "_pad", "type": "bytes", "length": 7 },
  { "name": "accountUid", "type": "uint64" }
]
```

## Complete Example

```json
{
  "protocol": {
    "name": "MMORPG Simulator",
    "version": "1.0",
    "endian": "little",
    "pack": 1,
    "header": {
      "size_field": "length",
      "type_field": "type",
      "fields": [
        { "name": "length", "type": "uint16", "offset": 0 },
        { "name": "type", "type": "uint16", "offset": 2 }
      ]
    }
  },
  "packets": [
    {
      "type": 257,
      "name": "CS_LOGIN",
      "direction": "C2S",
      "fields": [
        { "name": "accountId", "type": "string", "length": 32 },
        { "name": "password", "type": "string", "length": 32 }
      ]
    },
    {
      "type": 258,
      "name": "SC_LOGIN_RESULT",
      "direction": "S2C",
      "fields": [
        { "name": "success", "type": "uint8" },
        { "name": "accountUid", "type": "uint64" },
        { "name": "message", "type": "string", "length": 64 }
      ]
    }
  ]
}
```

## Checklist for New Protocols

1. **Endian 확인**: 대부분 little-endian
2. **Pack 확인**: 소스코드에서 `#pragma pack` 확인
3. **Header 구조 확인**: length/type 필드의 타입과 순서
4. **Header 크기 확인**: 자동 계산 또는 명시적 `size` 지정
5. **패킷별 필드 정의**: 구조체 정의와 일치하는지 확인
