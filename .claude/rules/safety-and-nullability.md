---
paths:
  - "src/**/*.cs"
---

# Safety & Nullability

## Unsafe Code
- **STRICTLY FORBIDDEN**
- Do NOT use the `unsafe` keyword
- Use safe low-level alternatives like `Span<T>`, `ReadOnlySpan<T>`, and `ref struct`

## Null-Forgiving Operator (`!`) in Production Code
- **STRICTLY FORBIDDEN**
- Never use `!` to silence nullable warnings
- Fix the logic or use `UnreachableException` instead

## Nullable Reference Types (NRT)
- Must be `enabled` in all projects
- Strictly follow nullable annotations
- Do not suppress warnings with `!` operator in production code
