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

### Pattern Matching & Type Handling
- Use `switch` expressions and `is` patterns (e.g., `is not null`)
- Use declaration patterns (`if (obj is Type t)`) or recursive patterns instead of the `as` operator
- ❌ The `as` operator is **STRICTLY FORBIDDEN** (except when required by external APIs)
- Use LINQ `OfType<T>()` for filtering and casting collections by type
- ❌ Do NOT use `Select(x => x as T).Where(x => x is not null)` or similar manual filtering patterns
- Avoid legacy `switch` statements or `==` checks for null

### Strings
- Use **Interpolated Strings** (`$"{var}"`)
- Avoid `String.Format`

### Disposal
- Use `using var` declarations

## Immutability
- Prefer **immutable by default**: data should flow through transformations rather than being mutated in place
- Mutable fields and mutable properties require justification; flag any that can be made immutable without meaningful cost

## Pure Functions
- Prefer **pure functions**: a method should compute and return its result rather than mutate state through `ref` or `out` parameters
- Return a tuple or a dedicated result type instead of using `ref`/`out` to propagate computed values back to the caller

### `out` parameters
- Use `out` **only** when implementing a `TryParse`-style method — i.e., when the method returns `bool` to signal success and needs to hand back a parsed value on success
- Do NOT use `out` for any other purpose; returning a `Result<T>` or tuple is always preferable

### `ref` parameters
- Use `ref` **only** as a performance optimization: when a large value-type (`struct`) would otherwise be copied repeatedly on every call, passing it by `ref` avoids that overhead
- Do NOT use `ref` to return computed values or to simulate multiple return values — use tuples or result types instead

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
- **ValueTuple element names**: use **camelCase** (e.g., `(string key, string value)`, `(int count, bool found)`). Tuple elements are destructured into local variables, so camelCase aligns with the local variable naming convention

## Project and Directory Placement
- Every class must reside in the project that matches its abstraction layer (`Engine` for core logic, `App` for TUI/presentation, `Tests` for test code)
- Even within the correct project, verify the directory is appropriate for the abstraction or domain the class belongs to — a misplaced file is a discoverability and maintainability problem
