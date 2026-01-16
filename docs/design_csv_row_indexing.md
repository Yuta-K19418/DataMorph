# CSV Row Indexing - Design Document

## Scope
This implementation focuses on **comma-only support** as a first step. Delimiter detection will be deferred to a future iteration.

---

## 1. Requirements

### Functional Requirements
- Index CSV files by row position (offset and length)
- Support comma (`,`) as the delimiter (fixed, no auto-detection)
- Handle quoted fields correctly (RFC 4180 compliant)
  - Quotes inside quoted fields are escaped as `""`
  - Delimiters and newlines inside quoted fields must be ignored
- Support both `\n` (LF) and `\r\n` (CRLF) line endings
- Provide efficient random access to any row by index

### Non-Functional Requirements
- **Zero allocations** during indexing (hot path)
- **Native AOT compatible** (no reflection)
- **Thread-safe** indexing (future-proof for parallel access)
- Efficient for large files (GB-scale)

---

## 2. Design

### 2.1 Class: `CsvRowIndexer`

#### Responsibilities
- Build an index of row positions in a CSV file
- Provide checkpoint-based row lookup for efficient seeking

#### Key Design Decisions

**Checkpoint Strategy:**
- Store checkpoints every **1000 rows** to balance memory usage and seek performance
- Each checkpoint stores the file offset of the row start
- `GetCheckPoint(targetRow)` returns `(byteOffset, rowOffset)` where:
  - `byteOffset`: File position of the nearest checkpoint
  - `rowOffset`: Number of rows to advance from the checkpoint

**Quote Handling:**
- Track quote state (`inQuotes` flag) while scanning
- Toggle the flag on each `"` character encountered
- Ignore delimiters and newlines when `inQuotes == true`
- This correctly handles:
  ```csv
  "field with, comma",normal
  "field with
  newline",normal
  "field with ""escaped"" quotes",normal
  ```

**CRLF Support:**
- Search for `\n` as the primary newline indicator
- When `\n` is found, check if the previous byte is `\r`
- The row offset should point to the character **after** the complete line ending (`\r\n` or `\n`)

**Buffering:**
- Use `ArrayPool<byte>.Shared` to rent a 1MB buffer (zero allocation)
- Read the file in chunks using `RandomAccess.Read`
- Process each buffer sequentially, tracking state across buffer boundaries

### 2.2 API Design

```csharp
public sealed class CsvRowIndexer
{
    public CsvRowIndexer(string filePath);

    // Build the row index (scans the entire file)
    public void BuildIndex();

    // Get the nearest checkpoint for a target row
    // Returns (byteOffset, rowOffset) where byteOffset is the file position
    // and rowOffset is the number of rows to advance from that checkpoint
    public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow);

    // Total number of rows indexed (updated every 1000 rows during BuildIndex)
    public long TotalRows { get; }
}
```

### 2.3 Thread Safety

**Concurrency Model:**
- `BuildIndex()` is **not thread-safe** (must be called once from a single thread)
- `GetCheckPoint()` is **thread-safe** (uses `lock` to protect checkpoint list access)
- `TotalRows` is **thread-safe** (uses `Interlocked` operations)

**Concurrent Access Scenario:**
- `BuildIndex()` can be running on Thread A while `GetCheckPoint()` and `TotalRows` are called from Thread B (e.g., TUI rendering)
- During indexing, `GetCheckPoint()` returns results based on the partially-constructed index
- `TotalRows` is updated periodically (every 1000 rows) for progress tracking

---

## 3. Implementation Plan

### 3.1 Core Algorithm

```
Input: CSV file path
Output: List of checkpoints (every 1000 rows)

1. Open file using SafeFileHandle via File.OpenHandle()
2. Rent a 1MB buffer from ArrayPool<byte>.Shared
3. Initialize state:
   - fileOffset = 0
   - rowCount = 0
   - inQuotes = false
   - checkpoints = [0]  // First checkpoint at file start
4. Loop until end of file:
   a. Read chunk into buffer using RandomAccess.Read()
   b. For each byte in buffer (using SearchValues.IndexOfAny for SIMD):
      - If byte == '"': toggle inQuotes flag
      - If byte == '\n' AND NOT inQuotes:
        * Increment rowCount
        * If rowCount % 1000 == 0:
          - Update TotalRows = rowCount (Interlocked.Exchange)
          - Add (fileOffset + current position + 1) to checkpoints (lock protected)
   c. Advance fileOffset by bytes read
5. If file doesn't end with newline AND not in quotes: increment rowCount
6. Return buffer to ArrayPool
7. Set final TotalRows = rowCount (Interlocked.Exchange)
```

### 3.2 Edge Cases

| Case | Handling |
|------|----------|
| Empty file | `TotalRows = 0`, checkpoints = `[0]` |
| Single row (header only, no trailing newline) | `TotalRows = 1`, checkpoints = `[0]` |
| No trailing newline | Last row is counted after main loop |
| Quoted field spanning buffers | `inQuotes` state persists across buffer reads |
| Mixed line endings (`\r\n` and `\n`) | Only `\n` is searched; `\r` is ignored (treated as data) |
| BuildIndex called during GetCheckPoint | GetCheckPoint uses partial index; TotalRows shows progress |

### 3.3 Performance Considerations

**Buffer Size:**
- 1MB buffer balances syscall overhead and memory usage
- Larger buffers reduce syscalls but increase memory pressure

**SearchValues Optimization:**
- Use `SearchValues<byte>` for SIMD-accelerated search of `\n` and `"`
- Pattern: `SearchValues.Create("\n\""u8)`

**Lock Granularity:**
- Lock only when adding checkpoints (every 1000 rows)
- Minimizes contention in multi-threaded scenarios

**Progress Tracking:**
- `TotalRows` is updated every 1000 rows during indexing
- Allows TUI to display real-time progress without blocking
- Uses `Interlocked.Exchange` to ensure thread-safe updates

---

## 4. Files to Modify/Create

| File | Action | Purpose |
|------|--------|---------|
| `src/Engine/IO/CsvRowIndexer.cs` | Create | Core indexing logic |
| `tests/DataMorph.Tests/IO/CsvRowIndexerTests.cs` | Create | Base test class with common setup |
| `tests/DataMorph.Tests/IO/CsvRowIndexerTests.BuildIndex.cs` | Create | BuildIndex unit tests |
| `tests/DataMorph.Tests/IO/CsvRowIndexerTests.GetCheckPoint.cs` | Create | GetCheckPoint unit tests |
| `tests/DataMorph.Tests/IO/CsvRowIndexerBenchmarks.cs` | Create | Performance benchmarks |

---

## 5. Testing Strategy

### 5.1 Unit Tests

**Test Cases:**
1. Simple CSV (comma-delimited, no quotes)
2. CSV with quoted fields containing commas
3. CSV with quoted fields containing newlines
4. CSV with escaped quotes (`""`)
5. CSV with CRLF line endings
6. CSV with mixed LF and CRLF endings
7. Empty file
8. Single row (header only)
9. Large file (10K+ rows) for checkpoint verification

### 5.2 Benchmarks

**Scenarios:**
- Small CSV: 1,000 rows (~50KB)
- Medium CSV: 100,000 rows (~5MB)
- Large CSV: 1,000,000 rows (~50MB)
- Complex CSV with quoted fields: 10,000 rows
- GetCheckPoint performance tests

**Metrics:**
- Time to build index
- Memory allocations (should be zero in hot path)
- Throughput (MB/s)

**Benchmark Configuration:**
- Run on both `.NET 8.0` and `NativeAot 8.0` runtimes
- Use `MemoryDiagnoser` to verify zero allocations
- Use `BenchmarkDotNet` with `[SimpleJob]` attribute

---

## 6. Future Work

### Phase 2: Delimiter Detection
- Implement auto-detection of `,`, `\t`, `;`, `|` delimiters
- Strategy:
  1. Read first 1024 bytes
  2. Count occurrences of each delimiter candidate on the first line
  3. Verify consistency across next 2-3 lines
  4. Select the most consistent delimiter

### Phase 3: Virtual Viewport Integration
- Use `CsvRowIndexer` to implement efficient row-based seeking
- Combine with `GetCheckPoint()` to minimize file reads for viewport rendering

---

## 7. Acceptance Criteria

- [ ] `CsvRowIndexer` correctly indexes CSV files with comma delimiter
- [ ] Handles quoted fields (commas, newlines, escaped quotes)
- [ ] Supports both `\n` and `\r\n` line endings
- [ ] Zero allocations in `BuildIndex()` hot path (verified by benchmark)
- [ ] Thread-safe `GetCheckPoint()` method
- [ ] 100% unit test coverage for edge cases
- [ ] Benchmarks demonstrate acceptable performance (>100 MB/s for large files)
- [ ] No compiler warnings
- [ ] Code passes `dotnet format` validation

---

## 8. Dependencies

- **RowIndexer**: This design opts for a specialized `CsvRowIndexer` to handle CSV-specific quote parsing. RowIndexer is line-oriented and doesn't account for quoted fields.
- **CRLF Support**: This implementation handles CRLF correctly by searching for `\n` and checking for preceding `\r`.

---

## 9. References

- RFC 4180: Common Format and MIME Type for CSV Files
