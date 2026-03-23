# Mermaid Diagram Patterns

## GitHub 네이티브 지원
- GitHub Markdown에서 ` ```mermaid ` 코드 블록 자동 렌더링
- VS Code: "Markdown Preview Mermaid Support" 확장으로 로컬 미리보기

## sequenceDiagram 유용 기능

### rect — 영역 구분
```
rect rgb(200, 220, 255)
    Note right of C: Phase Name
    C->>S: Request
    S->>C: Response
end
```
- 색상별 배경으로 의미 단위 구분
- rect 안에 loop, Note 등 중첩 가능

### loop — 반복 표시
```
loop ×N
    C->>S: Request
    S->>C: Response
end
```

### Note — 부가 정보
```
Note over S,C: 노이즈 패킷 [Noise]
Note right of C: Phase 이름
```
