---
paths:
  - "src/**/*.cs"
---

# Safety & Nullability

## Unsafe Code
- **STRICTLY FORBIDDEN**
- Do NOT use the `unsafe` keyword
- Use safe low-level alternatives like `Span<T>`, `ReadOnlySpan<T>`, and `ref struct`

## Recursion
- Prefer iteration over recursion by default
- Recursion is allowed only when the recursion depth is provably bounded and shallow (e.g., a fixed, small number of levels known at compile time) — not when depth depends on external input
- ❌ Do NOT use recursion to traverse structures whose depth is determined by untrusted or unbounded input — a `StackOverflowException` cannot be caught and crashes the process
- When traversal depth is unbounded, convert it to an iterative equivalent (e.g., a loop for linear cases, or an explicit stack for tree/graph-like traversals) instead

## Null-Forgiving Operator (`!`) in Production Code
- **STRICTLY FORBIDDEN**
- Never use `!` to silence nullable warnings
- Fix the logic or use `UnreachableException` instead

## Nullable Reference Types (NRT)
- Must be `enabled` in all projects
- Strictly follow nullable annotations
- Do not suppress warnings with `!` operator in production code

## Justify Every Nullable Annotation
- Every `?` annotation must have a clear reason for the value to be genuinely absent
- Unnecessary nullable annotations multiply code paths and bug surface — callers and the compiler must handle every `?`, so adding one without cause creates complexity with no benefit
- If there is no genuine reason a value can be absent, make it non-nullable and enforce the invariant at the boundary (constructor, factory, or validation)
