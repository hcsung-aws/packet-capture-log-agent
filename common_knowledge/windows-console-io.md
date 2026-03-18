# Windows Console I/O 통일성

## 원칙
콘솔 I/O 함수는 반드시 동일 계열로 통일. 서로 다른 버퍼를 사용하는 함수 혼용 금지.

## 입력 함수 계열
- **conio.h**: `_getch()`, `_getwch()`, `_kbhit()` — 콘솔 입력 버퍼 직접 접근
- **iostream**: `std::cin`, `std::getline` — C 런타임 내부 버퍼 경유

혼용 시 버퍼 불일치로 이전 입력 잔류 → 예상치 못한 동작.
불가피한 전환 시: `FlushConsoleInputBuffer(GetStdHandle(STD_INPUT_HANDLE))`

## UTF-8 출력
`SetConsoleOutputCP(65001)` + `setlocale(LC_ALL, ".UTF-8")` 병행 필수.
SetConsoleOutputCP 단독으로는 std::cout이 C 런타임 로케일(기본 ANSI)을 따르므로 불충분.

## 출처
Mickey 6 — mmorpg_simulator 채팅 기능에서 _getch/std::cin 혼용으로 IP가 채팅으로 전송되는 버그 + UTF-8 로그 깨짐 경험.
