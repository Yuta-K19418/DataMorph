---
paths:
  - "tests/**/*.cs"
---

# Testing

## Framework
- Use **xUnit** as the primary testing framework

## Null-Forgiving Operator (`!`) in Tests
- **ALLOWED ONLY** when explicitly testing validation logic that requires null inputs
- Primary use case: passing `null!` to verify `ArgumentNullException` or similar null-checking behavior
- Example:
  ```csharp
  var exception = Assert.Throws<ArgumentNullException>(() => Method(null!));
  ```
- Do NOT use `!` to work around test setup issues or lazy test data initialization
- If you need nullable values in tests for non-validation scenarios, use proper nullable types instead

## Assertions

### AwesomeAssertions
- Use **AwesomeAssertions** for general logic and high-level behavioral tests
- Provides more readable and expressive assertions
- Example: `result.Should().Be(expected);`

### Standard xUnit Asserts
- Use **Standard xUnit Asserts** (e.g., `Assert.Equal`) ONLY for tests intended to run in **Native AOT** environments
- Example: `Assert.Equal(expected, actual);`

## Performance Testing

### BenchmarkDotNet
- **BenchmarkDotNet** is **MANDATORY** for core engine components:
  - `MmapService`
  - `RowIndexer`
  - `Parser`
  - Other hot path components

### Native AOT Toolchain
- Benchmarks must be executed using the `NativeAot` toolchain
- This ensures performance measurements reflect real-world Native AOT deployment

## Coverage
- Focus on **100% coverage for the "Hot Paths"** (data processing logic)
- Prioritize core engine logic over UI state
