# WSL2 Git 주의사항

## git rebase 시 에디터 hang
WSL2 + Kiro CLI 환경에서 `git rebase --continue` 실행 시 에디터가 열리면서 hang 발생.
해결: `GIT_EDITOR=true git rebase --continue`

## 출처
Mickey 8 — rebase 중단 상태 해결 시 발견.
