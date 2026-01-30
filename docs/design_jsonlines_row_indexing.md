# JSON Lines Row Indexing - Design Document

## Scope
This document covers the row indexing implementation for JSON Lines (NDJSON) format. JSON Lines is a line-oriented format where each line contains a complete, independent JSON object.

---

## 1. Requirements

### Functional Requirements
- Index JSON Lines files by row position (offset and length)
- Handle both `\n` (LF) and `\r\n` (CRLF) line endings
- Provide efficient random access to any row by index
- Support background indexing while displaying first page

### Non-Functional Requirements
- **Zero allocations** during indexing (hot path)
- **Native AOT compatible** (no reflection)
- **Thread-safe** read access during indexing
- Efficient for large files (GB-scale)

---

## 2. Format Characteristics

### JSON Lines Specification
```jsonl
{"id": 1, "name": "Alice"}
{"id": 2, "name": "Bob"}
{"id": 3, "name": "Charlie"}
```

| Property | Description |
|----------|-------------|
| Line delimiter | `\n` or `\r\n` |
| Each line | Complete, valid JSON object |
| Independence | Each line is parseable in isolation |
| No header | Unlike CSV, no header row |

### Key Difference from CSV
- **No quoted field handling required**: JSON strings use escape sequences (`\"`, `\\n`) rather than literal newlines
- JSON specification requires control characters (including newlines) to be escaped within strings
- Therefore, a literal `\n` byte always indicates a record boundary

---

## 3. Design

### 3.1 Reuse Strategy

**Decision: Reuse `CsvDataRowIndexer` pattern with simplification**

The `CsvDataRowIndexer` handles:
1. Checkpoint-based indexing (every N rows)
2. Thread-safe progress tracking
3. CRLF support

For JSON Lines, we can simplify by removing:
- Quote state tracking (`inQuotes` flag)
- Header skipping logic

### 3.2 Class: `RowIndexer` (in `DataMorph.Engine.IO.JsonLines` namespace)

#### Responsibilities
- Build an index of row positions in a JSON Lines file
- Provide checkpoint-based row lookup for efficient seeking
- Track total row count for progress display

#### API Design

```csharp
namespace DataMorph.Engine.IO.JsonLines;

public sealed class RowIndexer
{
    public RowIndexer(string filePath);

    /// <summary>
    /// Builds the row index by scanning the entire file.
    /// NOT thread-safe - must be called once from a single thread.
    /// </summary>
    public void BuildIndex();

    /// <summary>
    /// Gets the nearest checkpoint for a target row.
    /// Thread-safe - can be called while BuildIndex() is running.
    /// </summary>
    /// <returns>(byteOffset, rowOffset) tuple</returns>
    public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow);

    /// <summary>
    /// Total rows indexed. Updated every 1000 rows during BuildIndex().
    /// Thread-safe via Interlocked operations.
    /// </summary>
    public long TotalRows { get; }

    /// <summary>
    /// The file path being indexed.
    /// </summary>
    public string FilePath { get; }
}
```

### 3.3 Key Design Decisions

**Checkpoint Strategy:**
- Store checkpoints every **1000 rows** (same as CSV)
- First checkpoint at byte offset 0 (start of file)
- Each checkpoint records the byte offset of a row start

**No Header Handling:**
- Unlike CSV, JSON Lines has no header row
- Row 0 is the first data record

**CRLF Support:**
- Search for `\n` as the primary newline indicator
- When calculating row start offset, account for preceding `\r`
- **Depends on #26 fix** for correct chunk boundary handling

**No Quote Tracking:**
- JSON strings escape newlines as `\n` (two characters: backslash + n)
- Literal newline bytes are always record boundaries
- Simpler and faster than CSV indexing

### 3.4 Thread Safety Model

| Operation | Thread Safety | Notes |
|-----------|---------------|-------|
| `BuildIndex()` | NOT thread-safe | Call once from a single thread |
| `GetCheckPoint()` | Thread-safe | Uses `lock` for checkpoint list |
| `TotalRows` | Thread-safe | Uses `Interlocked` operations |

**Concurrent Access Scenario:**
- Background thread runs `BuildIndex()`
- UI thread calls `GetCheckPoint()` and reads `TotalRows`
- Partial results are returned during indexing

---

## 4. Implementation Plan

### 4.1 Core Algorithm

```
Input: JSON Lines file path
Output: List of checkpoints (every 1000 rows)

1. Open file using SafeFileHandle via File.OpenHandle()
2. Rent a 1MB buffer from ArrayPool<byte>.Shared
3. Initialize state:
   - fileOffset = 0
   - rowCount = 0
   - checkpoints = [0]  // First checkpoint at file start
4. Loop until end of file:
   a. Read chunk into buffer using RandomAccess.Read()
   b. For each byte in buffer (using SearchValues for SIMD):
      - If byte == '\n':
        * Increment rowCount
        * If rowCount % 1000 == 0:
          - Update TotalRows (Interlocked.Exchange)
          - Add next row offset to checkpoints (lock protected)
   c. Advance fileOffset by bytes read
5. Handle last row if file doesn't end with newline
6. Return buffer to ArrayPool
7. Set final TotalRows (Interlocked.Exchange)
```

### 4.2 SearchValues Optimization

```csharp
private static readonly SearchValues<byte> _newline = SearchValues.Create("\n"u8);
```

Unlike CSV's `"\n\""u8`, JSON Lines only needs to search for newlines, making it slightly faster.

### 4.3 Edge Cases

| Case | Handling |
|------|----------|
| Empty file | `TotalRows = 0`, checkpoints = `[0]` |
| Single row, no trailing newline | `TotalRows = 1`, checkpoints = `[0]` |
| No trailing newline | Last row counted after main loop |
| Mixed `\r\n` and `\n` | Handle correctly (depends on #26) |
| Empty lines | Counted as rows (parser will handle) |

---

## 5. Files to Modify/Create

| File | Action | Purpose |
|------|--------|---------|
| `src/Engine/IO/JsonLines/RowIndexer.cs` | Create | Core indexing logic |
| `tests/DataMorph.Tests/IO/JsonLines/RowIndexerTests.cs` | Create | Base test class |
| `tests/DataMorph.Tests/IO/JsonLines/RowIndexerTests.BuildIndex.cs` | Create | BuildIndex unit tests |
| `tests/DataMorph.Tests/IO/JsonLines/RowIndexerTests.GetCheckPoint.cs` | Create | GetCheckPoint unit tests |
| `tests/DataMorph.Tests/IO/JsonLines/RowIndexerBenchmarks.cs` | Create | Performance benchmarks |

---

## 6. Testing Strategy

### 6.1 Unit Tests

**Test Cases:**
1. Simple JSON Lines (one object per line)
2. JSON Lines with CRLF line endings
3. JSON Lines with mixed LF and CRLF
4. Empty file
5. Single row without trailing newline
6. Large file (10K+ rows) for checkpoint verification
7. JSON with escaped newlines in strings (`"line1\\nline2"`)
8. Unicode content (UTF-8)

### 6.2 Benchmarks

**Scenarios:**
- Small: 1,000 rows (~100KB)
- Medium: 100,000 rows (~10MB)
- Large: 1,000,000 rows (~100MB)

**Metrics:**
- Time to build index
- Memory allocations (target: zero in hot path)
- Throughput (MB/s)

**Configuration:**
- Compare with CSV indexing performance
- Use `MemoryDiagnoser` to verify zero allocations
- Test on both managed and NativeAOT runtimes

---

## 7. Acceptance Criteria

- [ ] Correctly indexes JSON Lines files
- [ ] Handles both `\n` and `\r\n` line endings
- [ ] Zero allocations in `BuildIndex()` hot path
- [ ] Thread-safe `GetCheckPoint()` and `TotalRows`
- [ ] Unit tests for all edge cases
- [ ] Benchmarks demonstrate ≥100 MB/s throughput
- [ ] No compiler warnings
- [ ] Passes `dotnet format` validation

---

## 8. Future Integration

### Next Steps (Post-Implementation)
1. **Schema Scanner (#68)**: Analyze JSON structure across rows
2. **Virtual Viewport (#58)**: Display rows using indexed offsets
3. **Row Reader**: Parse JSON objects using indexed positions

### Integration with Existing Code
```
IO.JsonLines.RowIndexer (this issue)
        │
        ▼
IO.JsonLines.RowReader (future)
        │
        ▼
VirtualTableSource (existing pattern from CSV)
        │
        ▼
Terminal.Gui TableView
```

---

## 9. References

- [JSON Lines Specification](https://jsonlines.org/)
- [NDJSON Specification](http://ndjson.org/)
- Existing implementation: `IO/CsvDataRowIndexer.cs`
- Related issue: #26 (CRLF bug)
