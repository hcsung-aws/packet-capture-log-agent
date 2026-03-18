# Windows Console I/O

## I/O 함수 통일성 유지
- 콘솔 입력 함수는 반드시 한 계열로 통일할 것
  - conio.h 계열: `_getch()`, `_getwch()`, `_kbhit()`
  - iostream 계열: `std::cin`, `std::getline`
- 서로 다른 버퍼를 사용하므로 혼용 시 이전 입력 잔류, 예상치 못한 동작 발생
- 불가피하게 전환 시: `FlushConsoleInputBuffer(GetStdHandle(STD_INPUT_HANDLE))` 호출

## UTF-8 콘솔 설정
- 출력: `SetConsoleOutputCP(65001)` + `setlocale(LC_ALL, ".UTF-8")` 병행 필수
  - SetConsoleOutputCP만으로는 std::cout이 C 런타임 로케일을 따르므로 불충분
- 입력: `SetConsoleCP(65001)` + `_getwch()` → `WideCharToMultiByte(CP_UTF8)` 변환

## 한글 입력 (UTF-8)
- `_getwch()`: IME 조합 완료된 wchar_t 반환 → WideCharToMultiByte로 UTF-8 변환
- Backspace: UTF-8 멀티바이트 역추적 필요 (CJK=3바이트=2칸 폭)
