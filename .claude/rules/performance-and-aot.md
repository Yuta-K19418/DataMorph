---
paths:
  - "src/**/*.cs"
---

# Performance & Native AOT

## Performance & Memory (Hot Paths)

### Zero-Allocation
- Aim for **Zero-Allocation** in hot paths
- Prioritize `ReadOnlySpan<byte>`, `ref struct`, and `ArrayPool<T>`
- Do **not** construct temporary heap objects (`string`, `List<T>`, arrays) solely for a comparison or conditional check — prefer `ReadOnlySpan<T>` or `ReadOnlyMemory<T>` for intermediate processing; every avoidable allocation in a loop adds GC pressure

### Async Patterns
- Use **ValueTask** or **ValueTask<T>** to minimize heap allocations
- Especially important for high-frequency asynchronous operations:
  - Stream processing
  - TUI rendering updates
  - Methods likely to complete synchronously

### Buffers
- Use `ArrayPool<T>` for temporary large buffers
- Always return buffers to the pool after use

## Native AOT Compliance

### No Reflection
- `System.Reflection` and `System.Reflection.Emit` are **STRICTLY FORBIDDEN**
- These APIs are not compatible with Native AOT

### Source Generators (MANDATORY)
Source Generators must be used for:
- **JSON**: Use `JsonSourceGenerationContext` with `System.Text.Json`
- **Regex**: Use `[GeneratedRegex]` attribute
- **Dependency Injection**: Use source-generated DI where applicable

### AOT-Safe Patterns
- Avoid dynamic code generation
- Avoid runtime type inspection
- Use concrete types instead of dynamic dispatch where possible
