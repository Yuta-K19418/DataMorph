---
paths:
  - "src/**/*.cs"
---

# C# Coding Standards (.NET 10+ / C# 14 Strict)

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

## Immutability
- Prefer **immutable by default**: data should flow through transformations rather than being mutated in place
- Mutable fields and mutable properties require justification; flag any that can be made immutable without meaningful cost

## Structure & Complexity (STRICT)

### Class Size
- Target **under 300 lines**
- When a class is **approaching 200–300 lines**, proactively check whether multiple responsibilities have accumulated — it is easier to split early than after the class grows further
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
- **Consistency with existing code**: if existing types follow a naming convention (e.g., a specific suffix or prefix), new types must follow the same pattern — flag any new class, interface, or member whose name breaks the established convention in its namespace or layer

## Project and Directory Placement
- Every class must reside in the project that matches its abstraction layer (`Engine` for core logic, `App` for TUI/presentation, `Tests` for test code)
- Even within the correct project, verify the directory is appropriate for the abstraction or domain the class belongs to — a misplaced file is a discoverability and maintainability problem
