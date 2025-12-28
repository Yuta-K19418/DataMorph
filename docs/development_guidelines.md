## 1. Language Policy
- **Documentation**: All documentation (including README, specs, design docs, and TASKS.md) must be written in English.
- **Comments**: All code comments (inline `//` and XML documentation comments `///`) must be written in English.
- **Git**: Commit messages must follow the **Conventional Commits** specification and be written in English.
    - **Format**: `<type>: <description>`
    - **Types**:
        - `feat`: A new feature
        - `fix`: A bug fix
        - `docs`: Documentation only changes
        - `style`: Changes that do not affect the meaning of the code (white-space, formatting, etc)
        - `refactor`: A code change that neither fixes a bug nor adds a feature
        - `perf`: A code change that improves performance
        - `test`: Adding missing tests or correcting existing tests
        - `chore`: Changes to the build process or auxiliary tools and libraries
    - **Example**: `feat: add SIMD-accelerated newline detection`

## 2. C# Coding Standards
- **Modern Style**: 
    - Use **File-scoped namespaces**.
    - Use **Primary Constructors** for classes and structs where applicable.
    - Use `using var` declarations instead of nested `using` blocks.
- **Zero Warnings Policy**:
    - The project must compile with **zero warnings**.
    - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<AnalysisLevel>latest-all</AnalysisLevel>` must be enabled in `.csproj`.
    - Nullable Reference Types (NRT) must be `enabled` and strictly followed.
- **Error Handling**:
    - **Engine-level**: Use the `Result<T>` pattern for expected failures in hot paths to avoid exception overhead.
    - **Non-recoverable errors**: Use explicit, custom Exception types. Avoid generic `Exception`.
    - **Null handling**: Avoid the null-forgiving operator (`!`). Use `UnreachableException` for design-guaranteed non-null paths.
- **Performance & Memory**:
    - **Zero-Allocation**: Prioritize `ReadOnlySpan<byte>` and `ref struct` for data parsing.
    - **Async**: Use **ValueTask** or **ValueTask<T>** for high-frequency asynchronous operations (e.g., stream processing, TUI rendering updates) to minimize heap allocations, especially for methods likely to complete synchronously.
    - **Buffers**: Use `ArrayPool<T>` for temporary large buffers.
- **Native AOT Compliance**:
    - **No Reflection**: `System.Reflection` and `System.Reflection.Emit` are forbidden.
    - **Source Generators**: Mandatory use of Source Generators for JSON (`System.Text.Json`), Regex, and Dependency Injection.
- **Naming**: Follow standard .NET Naming Guidelines.

## 3. Testing
- **Framework**: Use **xUnit** as the primary testing framework.
- **Assertions**: 
    - Use **FluentAssertions** for general logic and high-level behavioral tests.
    - Use **Standard xUnit Asserts** for tests that are explicitly intended to run under **Native AOT** environments.
- **Performance Testing**:
    - **BenchmarkDotNet** is mandatory for core engine components (`MmapService`, `RowIndexer`, `Parser`).
    - Benchmarks must be executed using the `NativeAot` toolchain.
- **Coverage**: Focus on 100% coverage for the "Hot Paths" (data processing logic) rather than UI state.
