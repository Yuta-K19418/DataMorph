# MmapService Implementation

## Overview
MmapService provides efficient memory-mapped file access for the DataMorph Engine project. This service offers high-performance random access to large files using MemoryMappedFile with a safe, ArrayPool-based implementation.

## Architecture

### Design Decision: Instance Class with IDisposable
- MemoryMappedFile requires lifecycle management
- Instance class allows multiple concurrent file mappings
- Aligns with .NET BCL patterns (FileStream, etc.)
- No unsafe code - uses ArrayPool<byte> for buffer management

### Core API

```csharp
public sealed class MmapService : IDisposable
{
    // Factory method with Result<T> error handling
    public static Result<MmapService> Open(string filePath, FileAccess access = FileAccess.Read);

    // Fast path - throws on invalid range, copies data into provided span
    public void Read(long offset, Span<byte> destination);

    // Safe path - returns ValueTuple with success/error
    public (bool success, string error) TryRead(long offset, Span<byte> destination);

    // File metadata
    public long Length { get; }

    // Resource cleanup
    public void Dispose();
}
```

### Error Handling Strategy

**Use Result<T> for:**
- File open failures (not found, access denied, empty file)

**Use ValueTuple (bool, string) for:**
- Read validation failures in TryRead (out of bounds, disposed)
- Returns (true, string.Empty) on success
- Returns (false, "error message") on failure

**Use exceptions for:**
- Invalid arguments (ArgumentNullException, ArgumentOutOfRangeException)
- ObjectDisposedException (using after disposal)
- Read fast path (ArgumentOutOfRangeException, IOException)

## Implementation Steps

### 1. Create Directory Structure
```
src/Engine/IO/
  └── MmapService.cs

tests/DataMorph.Tests/IO/
  ├── MmapServiceTests.cs
  └── MmapServiceBenchmarks.cs
```

### 2. Implement MmapService.cs

**File:** `src/Engine/IO/MmapService.cs`

**Key components:**
1. Private fields:
   - `MemoryMappedFile _mmf`
   - `MemoryMappedViewAccessor _accessor`
   - `long _length`
   - `bool _disposed`

2. Private constructor (used by Open factory method)

3. `Open()` static factory method:
   - Validate file path (ArgumentException.ThrowIfNullOrWhiteSpace)
   - Check file exists using FileInfo
   - Check file is not empty
   - Create FileStream with FileOptions.RandomAccess
   - Create MemoryMappedFile.CreateFromFile
   - Create ViewAccessor
   - Use try-catch-finally to dispose resources on error
   - Wrap all I/O exceptions in Results.Failure<T>
   - Return Results.Success on success

4. `Read()` method (safe implementation):
   - Check not disposed (ObjectDisposedException.ThrowIf)
   - Validate offset >= 0 (ArgumentOutOfRangeException.ThrowIfNegative)
   - Return early if destination.Length == 0
   - **Overflow-safe range validation**: Use `offset > _length - destination.Length` instead of `offset + destination.Length > _length` to prevent arithmetic overflow
   - Rent buffer from ArrayPool<byte>.Shared
   - Use _accessor.ReadArray() to read data into buffer
   - Copy buffer to destination span
   - Return buffer to pool in finally block

5. `TryRead()` safe method:
   - Check all preconditions (_disposed, offset < 0)
   - **Overflow-safe range validation**: Use `destination.Length > 0 && offset > _length - destination.Length` to prevent arithmetic overflow
   - Return (false, "error message") on validation failures
   - Call Read() in try-catch
   - Return (true, string.Empty) on success
   - Return (false, exception message) on IOException, ObjectDisposedException, ArgumentOutOfRangeException

6. `Length` property:
   - Check not disposed (ObjectDisposedException.ThrowIf)
   - Return _length

7. `Dispose()`:
   - Check _disposed flag (return if already disposed)
   - Dispose _accessor
   - Dispose _mmf
   - Set _disposed = true

8. XML documentation for all public members

### 3. Implement Unit Tests

**File:** `tests/DataMorph.Tests/IO/MmapServiceTests.cs`

**Framework:** xUnit with FluentAssertions

**Test cases:**

Happy path (8 tests):
- `Open_ValidFile_ReturnsSuccess()` - Verify successful file opening
- `Read_ValidRange_ReadsCorrectData()` - Read partial data
- `Read_FullFile_ReadsCorrectData()` - Read entire file
- `Read_ZeroLength_DoesNotThrow()` - Empty span handling
- `Read_OffsetAtEnd_ZeroLengthDoesNotThrow()` - Edge case
- `TryRead_ValidRange_ReturnsSuccess()` - Successful TryRead
- `Length_AfterOpen_ReturnsFileSize()` - Metadata access

Error path - Open (3 tests):
- `Open_NonExistentFile_ReturnsFailure()` - File not found
- `Open_EmptyFile_ReturnsFailure()` - Zero-length file
- `Open_NullFilePath_ThrowsArgumentNullException()` - Null validation
- `Open_EmptyFilePath_ThrowsArgumentException()` - Empty string validation
- `Open_WhitespaceFilePath_ThrowsArgumentException()` - Whitespace validation

Error path - Read (4 tests):
- `Read_NegativeOffset_ThrowsArgumentOutOfRangeException()` - Invalid offset
- `Read_OutOfRange_ThrowsArgumentOutOfRangeException()` - Buffer too large
- `Read_OffsetPlusLengthOutOfRange_ThrowsArgumentOutOfRangeException()` - Range overflow
- `Read_LargeOffsetAndLength_ThrowsArgumentOutOfRangeException()` - Arithmetic overflow protection

Error path - TryRead (3 tests):
- `TryRead_NegativeOffset_ReturnsFailure()` - Returns (false, error)
- `TryRead_OutOfRange_ReturnsFailure()` - Range validation
- `TryRead_LargeOffsetAndLength_ReturnsFailure()` - Arithmetic overflow protection

Disposal (5 tests):
- `TryRead_AfterDispose_ReturnsFailure()` - TryRead on disposed service
- `Length_AfterDispose_ThrowsObjectDisposedException()` - Property access after dispose
- `Read_AfterDispose_ThrowsObjectDisposedException()` - Read after dispose
- `Dispose_CalledMultipleTimes_DoesNotThrow()` - Idempotent dispose

**Total:** 24 tests

**Test fixture pattern:**
- Implements IDisposable for temp file cleanup
- Creates test file in constructor
- Deletes test file in Dispose()
- Uses `using var service = result.Value;` for resource management

### 4. Implement Performance Benchmarks

**File:** `tests/DataMorph.Tests/IO/MmapServiceBenchmarks.cs`

**Add dependency:** BenchmarkDotNet to DataMorph.Tests.csproj

**Benchmarks:**
- `Read_SmallChunk()` - 1KB reads (baseline)
- `Read_MediumChunk()` - 64KB reads
- `Read_LargeChunk()` - 1MB reads
- `TryRead_WithValidation()` - validation overhead

**Configuration:**
- `[MemoryDiagnoser]` - track allocations
- `[SimpleJob(RuntimeMoniker.Net80)]` - .NET 8.0 target
- Create 10MB test file in constructor with Random.Shared
- Use GlobalSetup/GlobalCleanup for MmapService lifecycle
- Consume data to prevent dead code elimination (sum bytes)

**Expected performance:**
- ArrayPool<byte> allocation is minimal (only internal pool management)
- Copying overhead from ReadArray to destination span
- Memory bandwidth-limited for large chunks

### 5. Testing & Validation

1. Run unit tests: `dotnet test`
   - Verify 100% pass rate
   - Check code coverage for MmapService

2. Run benchmarks: `dotnet run -c Release --project tests/DataMorph.Tests`
   - Verify zero allocations in GetSpan
   - Document baseline performance

3. Verify code quality:
   - Zero compiler warnings
   - Passes `dotnet format --verify-no-changes`
   - Follows .editorconfig rules

4. Native AOT compatibility:
   - Build passes with IsAotCompatible=true
   - No trimming warnings

## Key Design Considerations

### Safe Memory Management
- **No unsafe code** - uses ArrayPool<byte> for buffer management
- Caller provides destination Span<byte> for zero-copy semantics
- ArrayPool reduces allocation pressure
- Data copied from accessor to caller's span
- Buffer returned to pool immediately after use
- **Overflow-safe arithmetic**: Range validation uses `offset > _length - destination.Length` instead of `offset + destination.Length > _length` to prevent integer overflow attacks

### Thread Safety
- MemoryMappedFile is thread-safe for concurrent reads
- Multiple MmapService instances can map the same file
- Read/TryRead methods are thread-safe
- Dispose should only be called once, by the owning thread

### Resource Management
- **Follow RAII pattern:** Use `using var service = MmapService.Open(path).Value;`
- Dispose accessor before mmf in Dispose()
- Make Dispose() idempotent (safe to call multiple times)
- Use FileShare.Read to allow concurrent file access
- Result<T> should not be stored in fields - access .Value in same scope

### Error Handling
- **Result<T>** for expected failures (file not found, access denied, empty file)
- **ValueTuple (bool, string)** for TryRead validation (non-nullable error string)
- **Exceptions** for programming errors (ArgumentException, ObjectDisposedException)
- Descriptive error messages with context (offsets, lengths, ranges)

## Success Criteria

**Functional:**
- ✓ Open files with memory mapping
- ✓ Read data into caller-provided Span<byte>
- ✓ Handle errors with Result<T> and ValueTuple
- ✓ Implement IDisposable correctly
- ✓ Zero compiler warnings
- ✓ Safe implementation without unsafe code

**Performance:**
- ✓ Minimal allocations (only ArrayPool internal management)
- ✓ Efficient data copying from memory-mapped region
- ✓ Memory bandwidth-limited throughput for large reads

**Quality:**
- ✓ Complete XML documentation for all public members
- ✓ 24 comprehensive unit tests with FluentAssertions
- ✓ Includes overflow protection test cases
- ✓ Performance benchmarks with BenchmarkDotNet
- ✓ Native AOT compatible (no unsafe code)
- ✓ Follows .editorconfig coding standards
- ✓ Zero-warning policy enforced

## Usage Examples

### Basic Usage Pattern

```csharp
// Open a file with memory mapping
var result = MmapService.Open("data.csv");
if (result.IsFailure)
{
    Console.WriteLine($"Failed to open file: {result.Error}");
    return;
}

// IMPORTANT: Use 'using var' to ensure proper disposal
using var service = result.Value;

// Read data into a span
Span<byte> buffer = stackalloc byte[1024];
service.Read(0, buffer);

// Process the data
string text = Encoding.UTF8.GetString(buffer);
```

### Safe Reading with TryRead

```csharp
using var service = MmapService.Open("data.csv").Value;

Span<byte> buffer = stackalloc byte[1024];
var (success, error) = service.TryRead(offset: 100, buffer);

if (success)
{
    // Process buffer
    ProcessData(buffer);
}
else
{
    Console.WriteLine($"Read failed: {error}");
}
```

### Reading File Metadata

```csharp
using var service = MmapService.Open("data.csv").Value;

// Get file size
long fileSize = service.Length;
Console.WriteLine($"File size: {fileSize} bytes");
```

## Critical Files

1. **src/Engine/IO/MmapService.cs** - Core implementation (220 lines)
2. **src/Engine/Results.cs** - Factory methods (existing)
3. **src/Engine/Result.cs** - Result types (existing)
4. **tests/DataMorph.Tests/IO/MmapServiceTests.cs** - Unit tests (24 tests including overflow protection)
5. **tests/DataMorph.Tests/IO/MmapServiceBenchmarks.cs** - Performance benchmarks
6. **.editorconfig** - Test-specific analyzer suppressions

## Future Integration

MmapService will be consumed by:
- **RowIndexer** - Jump to row byte offsets
- **Utf8JsonReader** - Parse JSON from spans
- **CSV Parser** - SIMD-accelerated delimiter scanning
- **ViewManager** - Request viewport data ranges
