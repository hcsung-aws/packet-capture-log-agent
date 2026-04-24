# C# Async Patterns

## Task.Run 내부 예외
- Task.Run 내부의 async 예외는 외부에서 관찰되지 않음 (fire-and-forget)
- 반드시 내부 try-catch로 예외 처리 필요
- 또는 Task를 await하여 예외 전파

## Last Updated
2026-04-17
