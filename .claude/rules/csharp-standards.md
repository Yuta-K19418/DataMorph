---
paths:
  - "src/**/*.cs"
---

# C# Coding Standards (.NET 8.0+ / C# 12 Strict)

## Modern Syntax (MANDATORY)

### Namespaces
- Use **File-scoped namespaces** (`namespace X;`)
- ❌ Block-scoped namespaces (`namespace X { ... }`) are **STRICTLY FORBIDDEN**

### Constructors
- Use **Primary Constructors** for classes/structs/records (unless explicit validation requires a body)

### Collections
- Use **Collection Expressions** `[]`
- Example: `int[] x = [1, 2];`
- ❌ `new List<T>()` or `new T[] { ... }` are **STRICTLY FORBIDDEN**

### Pattern Matching
- Use `switch` expressions and `is` patterns (e.g., `is not null`)
- Avoid legacy `switch` statements or `==` checks for null

### Strings
- Use **Interpolated Strings** (`$"{var}"`)
- Avoid `String.Format`

### Disposal
- Use `using var` declarations

## Structure & Complexity (STRICT)

### Class Size
- Target **under 300 lines**
- If a class exceeds this, strictly review for **Single Responsibility Principle (SRP)** violations
- If multiple responsibilities are detected, refactor by splitting the class

### No `else` Clause
- Do NOT use `else` clauses
- Use **Guard Clauses** (early return) or `continue` to keep the logic flat

### Max Nesting
- Limit indentation to a maximum of **2 levels**
- If logic requires deeper nesting, refactor by extracting methods

## Zero Warnings Policy
- The project must compile with **zero warnings**
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<AnalysisLevel>latest-all</AnalysisLevel>` must be enabled in `.csproj`
- Nullable Reference Types (NRT) must be `enabled` and strictly followed

## Error Handling
- **Engine-level**: Use the `Result<T>` pattern for expected failures in hot paths to avoid exception overhead
- Do NOT use Exceptions for flow control
- **Non-recoverable errors**: Use explicit, custom Exception types. Avoid generic `Exception`

## Legacy Patterns (STRICTLY FORBIDDEN)
- ❌ `new List<T>()` or `new T[] { ... }` (Use `[]`)
- ❌ Block-scoped namespaces (`namespace X { ... }`)
- ❌ `System.Reflection` and `System.Reflection.Emit` (Due to Native AOT constraints)

## Naming
- Follow standard .NET Naming Guidelines
