# Commands
- Build: dotnet build
- Test: dotnet test
- Format: dotnet format

# Project Guidelines

## Language & Git Policy
- **Language:** English ONLY for all documentation, comments, and commit messages.
- **Commits:** Follow **Conventional Commits** specification (e.g., `feat:`, `fix:`, `perf:`, `refactor:`, `test:`).
- **Workflow:**
  - ALWAYS run `dotnet format` and `dotnet test` BEFORE committing.
  - Ensure the project compiles with **Zero Warnings** (`TreatWarningsAsErrors` is enabled).

## C# Coding Standards (.NET 8.0+ / C# 12 Strict)
- **Modern Syntax (MANDATORY):**
  - **Namespaces:** Use File-scoped namespaces (`namespace X;`).
  - **Constructors:** Use **Primary Constructors** for classes/structs/records (unless explicit validation requires a body).
  - **Collections:** Use **Collection Expressions** `[]` (e.g., `int[] x = [1, 2];`).
  - **Pattern Matching:** Use `switch` expressions and `is` patterns (e.g., `is not null`) instead of legacy `switch` statements or `==` checks.
  - **Strings:** Use Interpolated Strings (`$"{var}"`) instead of `String.Format`.
  - **Disposal:** Use `using var` declarations.

- **Structure & Complexity (STRICT):**
  - **Class Size:** Target **under 300 lines**. If a class exceeds this, strictly review for **Single Responsibility Principle (SRP)** violations. If multiple responsibilities are detected, refactor by splitting the class.
  - **No `else`:** Do NOT use `else` clauses. Use **Guard Clauses** (early return) or `continue` to keep the logic flat.
  - **Max Nesting:** Limit indentation to a maximum of **2 levels**. If logic requires deeper nesting, refactor by extracting methods.

- **Legacy Patterns (STRICTLY FORBIDDEN):**
  - ❌ `new List<T>()` or `new T[] { ... }` (Use `[]`).
  - ❌ Block-scoped namespaces (`namespace X { ... }`).
  - ❌ `System.Reflection` and `System.Reflection.Emit` (Due to Native AOT constraints).

## Safety & Nullability
- **Unsafe Code:** **STRICTLY FORBIDDEN**. Do NOT use the `unsafe` keyword. Use safe low-level alternatives like `Span<T>`, `ReadOnlySpan<T>`, and `ref struct`.
- **Null-Forgiving Operator (`!`) Rules:**
  - **Production Code:** **STRICTLY FORBIDDEN**. Never use `!` to silence nullable warnings. Fix the logic or use `UnreachableException`.
  - **Test Code:** **ALLOWED ONLY** when explicitly testing validation logic (e.g., passing `null!` to verify `ArgumentNullException`).

## Architecture & Native AOT
- **Performance & Memory (Hot Paths):**
  - **Allocations:** Aim for **Zero-Allocation**. Prioritize `ReadOnlySpan<byte>`, `ref struct`, and `ArrayPool<T>`.
  - **Async:** Use `ValueTask` or `ValueTask<T>` to minimize heap allocations.
  - **Error Handling:** Use the `Result<T>` pattern for expected failures. Do NOT use Exceptions for flow control.
  - **Source Generators:** MANDATORY for JSON (`JsonSourceGenerationContext`), Regex, and Dependency Injection.

## Testing
- **Framework:** xUnit.
- **Assertions:**
  - Use **FluentAssertions** for general logic and behavior tests.
  - Use **Standard xUnit Asserts** (e.g., `Assert.Equal`) ONLY for tests intended to run in **Native AOT** environments.
- **Benchmarks:** Use **BenchmarkDotNet** with the `NativeAot` toolchain for core engine components.
