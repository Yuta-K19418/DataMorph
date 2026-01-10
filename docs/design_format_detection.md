# Format Detection Design Document

## Overview
Implement automatic format detection to determine the data format (CSV, JSON Lines, JSON Array, JSON Object) from file content and route to the appropriate processing pipeline.

## Requirements

### Format Detection Rules
| Format | Detection Logic | Initial Mode | Next Step |
|--------|-----------------|--------------|-----------|
| **CSV** | Non-JSON format validated by Sep library (requires ≥2 columns) | Table (Grid) | CSV Row Indexing |
| **JSON Lines** | First non-whitespace is `{`, followed by another root-level object | Table (Grid) | JSON Lines Row Indexing |
| **JSON Array** | Starts with `[` | Explorer (Tree) | JSON Array Element Indexing |
| **JSON Object** | Starts with `{`, single root-level object only | Explorer (Tree) | JSON Object Key Indexing |

### Implementation Constraints
- Use `PipeReader` for streaming and buffer management
- Use `ReadOnlySequence<byte>` for zero-allocation detection
- Return `Result<DataFormat>` for error handling
- Handle edge cases (empty files, whitespace-only, malformed content, large files)

## Design

### 1. Update DataFormat Enum
Current enum only has `Json` and `Csv`. Need to expand to 4 formats:
```csharp
public enum DataFormat
{
    Csv,
    JsonLines,
    JsonArray,
    JsonObject
}
```

### 2. FormatDetector Class
Location: `src/Engine/IO/FormatDetector.cs`

```csharp
public static class FormatDetector
{
    public static async ValueTask<Result<DataFormat>> Detect(
        Func<Stream> createStream,
        CancellationToken cancellationToken = default
    );
}
```

**Key Design Decision**: Accept `Func<Stream>` factory instead of a single `Stream` instance to support multiple stream reads (needed for CSV validation after initial classification).

### 3. Detection Algorithm (3-Phase)

#### Phase 1: Skip BOM and Whitespace (First Loop)
- Use `PipeReader` to read stream incrementally
- Use `SequenceReader<byte>` to skip UTF-8 BOM (0xEF 0xBB 0xBF)
- Skip leading whitespace (space, tab, CR, LF)
- Handle edge cases:
  - Empty file → Error: "File is empty"
  - Whitespace only → Error: "File contains only whitespace"
  - Incomplete buffer → Continue reading until first valid character found

#### Phase 2: Classify by First Character and Validate
```
firstChar = buffer.First()

if firstChar == '[':
    return JsonArray  // Immediate result

if firstChar == '{':
    return null  // Proceed to Phase 3 (JSON processing)

else:
    // Assume CSV, validate with Sep library
    ValidateCsvFormat():
      - Create new stream via createStream() factory
      - Parse CSV header using Sep library
      - Validate column count ≥ 2
      - Return success or error
```

**Note**: CSV validation occurs in Phase 2 because we need an immediate result for non-JSON formats.

#### Phase 3: JSON Processing (Second Loop - JsonObject vs JsonLines)
For files starting with `{`, use `Utf8JsonReader` to distinguish:

```
Parse JSON with Utf8JsonReader:
  - Track completedFirstObject flag
  - When CurrentDepth == 0 && TokenType == EndObject:
      completedFirstObject = true

  On JsonException:
    if completedFirstObject:
        return JsonLines  // Second root object detected
    else:
        return Error  // Malformed JSON

  On successful completion (no exception):
    return JsonObject  // Only one root object
```

**Key Insight**: `Utf8JsonReader` throws `JsonException` when it encounters a second root-level object, which we use to detect JSON Lines format.

### 4. Edge Cases

| Case | Behavior |
|------|----------|
| Empty file | Return Error: "File is empty" |
| Whitespace only | Return Error: "File contains only whitespace" |
| Malformed JSON | Return Error: "Invalid JSON format: {exception message}" |
| Nested objects in JSON | Properly handled by tracking `CurrentDepth` in `Utf8JsonReader` |
| Single-column CSV | Return Error: "Invalid CSV format: requires at least 2 columns" |
| Large files (>4KB first object) | Handled by `PipeReader` buffer management and stateful JSON parsing |
| BOM (UTF-8) | Skip BOM bytes (0xEF 0xBB 0xBF) using `SequenceReader.IsNext()` |
| XML/YAML content | Detected as CSV, then fails validation with helpful error message |

### 5. Zero-Allocation Strategy
- Use `PipeReader` for efficient streaming with minimal allocations
- Use `ReadOnlySequence<byte>` and `SequenceReader<byte>` for buffer operations
- `Utf8JsonReader` operates directly on byte sequences without string allocations
- Static readonly `_supportedFormatNames` computed once at class initialization
- No LINQ allocations in hot path (only in error messages)

## Implementation Plan

### Files Modified
1. `src/Engine/Types/DataFormat.cs` - Updated enum with 4 formats
2. `src/Engine/Types/DataFormatExtensions.cs` - Added `GetDisplayName()` extension method
3. `src/Engine/IO/FormatDetector.cs` - New implementation
4. `src/Engine/DataMorph.Engine.csproj` - Added Sep package reference
5. `.editorconfig` - Added naming rule for const fields

### Files Created (Tests)
1. `tests/DataMorph.Tests/IO/FormatDetectorTests.cs` - Comprehensive unit tests (575 lines)

### Dependencies
- `System.IO.Pipelines` - Built-in .NET library for efficient stream processing
- `System.Text.Json` - Built-in .NET library for JSON parsing (Native AOT compatible)
- `Sep` (v0.12.1) - **Native AOT compatible** CSV parser library
  - Trimmable and AOT/NativeAOT compatible
  - Zero allocation design using `Span<T>` and `ArrayPool<T>`
  - No reflection or dynamic code generation
- `Result<T>` - Already implemented

## Test Cases

### JSON Array
```json
[
  {"id": 1},
  {"id": 2}
]
```
Expected: `DataFormat.JsonArray`

### JSON Object
```json
{
  "data": [...],
  "meta": {...}
}
```
Expected: `DataFormat.JsonObject`

### JSON Lines
```json
{"id": 1}
{"id": 2}
{"id": 3}
```
Expected: `DataFormat.JsonLines`

### CSV
```csv
id,name,age
1,Alice,30
2,Bob,25
```
Expected: `DataFormat.Csv`

### Edge Cases
- Leading whitespace before `[` or `{`
- BOM markers
- Single-line JSON object (no newlines): `{"a":1,"b":2}` → JsonObject
- Empty `{}` or `[]`
- Files with only whitespace

## Performance Considerations
- **Streaming approach**: Uses `PipeReader` to read incrementally, not loading entire file
- **JSON Array detection**: O(1) - Single character check after skipping whitespace
- **JSON Object/Lines detection**: O(n) where n = size of first JSON object
  - Uses stateful `Utf8JsonReader` to handle objects larger than buffer size
  - Continues reading until first object completes or second object detected
- **CSV detection**: O(m) where m = header row size (Sep library validates structure)
- **Zero allocations** in critical path via `ReadOnlySequence<byte>` and `SequenceReader<byte>`
- **Sub-millisecond detection** for typical files
- **Large file support**: Handles files of any size without loading into memory

## Native AOT Compatibility
All components are fully compatible with Native AOT:
- ✅ `System.IO.Pipelines` - No reflection
- ✅ `System.Text.Json` (`Utf8JsonReader`) - No reflection, operates on byte sequences
- ✅ `Sep` library - Explicitly designed for Native AOT, no reflection or dynamic code generation
- ✅ No use of `System.Reflection` or `System.Reflection.Emit`
- ✅ All exceptions are concrete types (no dynamic exception generation)
