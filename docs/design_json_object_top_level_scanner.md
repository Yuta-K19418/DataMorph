# JSON Object Top-Level Key Scanner — Design Document

## Scope

Implement `TopLevelScanner`, the Engine-layer class that scans all top-level keys and their
raw byte values from a JSON Object file
(`{"key1": ..., "key2": ..., ...}`).

The App-layer integration (tree view, file dialog wiring) is out of scope and will be
addressed in the follow-up issue (Explorer Mode).

---

## 1. Requirements

### Functional Requirements

- Scan all top-level keys and their values from a JSON Object file
- Return results as `IReadOnlyList<(string key, ReadOnlyMemory<byte> value)>` preserving
  insertion order
- Correctly handle nested objects/arrays as values without parsing deep structures
- Duplicate keys: last value wins (consistent with `JSON.parse`); key order reflects first occurrence

### Non-Functional Requirements

- **One-shot**: scan once at call time; no persistent random-access indexer required
- **Streaming**: `RandomAccess.Read` + `Utf8JsonReader`; file is never fully loaded into memory
- **AOT-compatible**: `Utf8JsonReader` is AOT-safe; no reflection
- **Adaptive buffer**: starts at 1 MB, doubles on demand up to 16 MB; throws
  `NotSupportedException` beyond that

---

## 2. Why `IRowIndexer` is Not Needed

| Aspect | JSON Lines / JSON Array | JSON Object |
|--------|------------------------|-------------|
| Access pattern | Random access for scroll / lazy load | Sequential scan once at file open |
| Persistent resource | `IRowIndexer` kept alive for the session | Scanner is discarded after scan |
| Key count | Millions of rows | Tens to hundreds of top-level keys |
| Interface fit | `GetCheckPoint(long targetRow)` — row-based | No row concept; key-based, one-time |

JSON Object top-level keys are the primary navigation structure. After the scan, all
top-level values are held in memory by the caller; further access is in-memory only.

### Value Memory Strategy

All top-level values are copied into `byte[]` during the scan. The caller's child nodes can
use `ReadOnlyMemory<byte>.Slice` into the parent's buffer, so no additional allocations occur
when expanding nested structures.

The alternative of storing `(fileOffset, length)` and loading lazily was rejected: JSON
Object files have few top-level keys (not millions), so the memory savings are negligible
while the design complexity (new lazy-loading node types) is significant.

---

## 3. Design

### 3.1 Class: `JsonObject.TopLevelScanner`

```csharp
namespace DataMorph.Engine.IO.JsonObject;

internal sealed class TopLevelScanner
{
    private const int InitialBufferSize = 1024 * 1024;      // 1 MB
    private const int MaxBufferSize     = 16 * 1024 * 1024; // 16 MB

    internal static IReadOnlyList<(string key, ReadOnlyMemory<byte> value)> Scan(
        string filePath,
        CancellationToken ct = default);
}
```

- Uses `RandomAccess.Read` + `Utf8JsonReader` with `JsonReaderState` carried across chunks
  (same rolling-buffer pattern as `JsonArray.RowIndexer`)
- Reads tokens until depth-1 `PropertyName` to capture a key
- Reads the following value token(s) and copies their bytes into a `byte[]`
- Returns when the root `EndObject` token is consumed

### 3.2 Scanning Algorithm

```
fileSize = new FileInfo(filePath).Length
buffer = ArrayPool.Rent(InitialBufferSize)
try:
  using handle = File.OpenHandle(filePath, Open, Read, Read)
  state = default(JsonReaderState)
  bufferOriginFileOffset = 0L
  fileReadOffset = 0L
  remainingLen = 0
  result = new List<(string, ReadOnlyMemory<byte>)>()
  keyIndex = new Dictionary<string, int>()   // key → index in result (for last-wins dedup)
  currentKey = null
  valueStart = -1L

loop:
  ct.ThrowIfCancellationRequested()

  // Detect stuck: buffer is full and nothing was consumed last pass
  if remainingLen == buffer.Length:
    if buffer.Length >= MaxBufferSize:
      throw NotSupportedException("JSON string value exceeds maximum supported size (16 MB).")
    buffer = GrowBuffer(buffer)   // double size; copy remaining bytes; return old buffer to pool

  bytesRead = RandomAccess.Read(handle, buffer[remainingLen..], fileReadOffset)
  if bytesRead == 0: break
  fileReadOffset += bytesRead
  dataEnd = remainingLen + bytesRead
  isFinalBlock = (fileReadOffset >= fileSize)

  reader = new Utf8JsonReader(buffer[0..dataEnd], isFinalBlock, state)

  innerLoop:
    if not reader.Read(): break innerLoop

    depth = reader.CurrentDepth

    if depth == 0 && reader.TokenType == EndObject: goto done  // root object closed
    if depth != 1: continue innerLoop                          // skip nested tokens

    // depth 1: PropertyName or value tokens

    if reader.TokenType == PropertyName:
      currentKey = reader.GetString()
      valueStart = -1L
      continue innerLoop

    // Value token for currentKey
    if currentKey is null: continue innerLoop   // malformed JSON; skip

    if reader.TokenType in {StartObject, StartArray}:
      if valueStart < 0:
        valueStart = bufferOriginFileOffset + reader.TokenStartIndex
      continue innerLoop   // wait for matching End* to return to depth 1

    if reader.TokenType in {EndObject, EndArray}:
      // reader.BytesConsumed is the position immediately after the closing `}`/`]`
      valueEnd = bufferOriginFileOffset + (long)reader.BytesConsumed
      RecordEntry(currentKey, buffer, valueStart, valueEnd)
      currentKey = null
      valueStart = -1L
      continue innerLoop

    // Primitive value at depth 1 (String, Number, True, False, Null)
    // TopLevelScanner always constructs Utf8JsonReader from ReadOnlySpan<byte>,
    // so HasValueSequence is always false — use ValueSpan exclusively.
    primitiveStart = bufferOriginFileOffset + reader.TokenStartIndex
    primitiveEnd   = bufferOriginFileOffset + (long)reader.BytesConsumed
    RecordEntry(currentKey, buffer, primitiveStart, primitiveEnd)
    currentKey = null
    valueStart = -1L

  state = reader.CurrentState

  // Do not advance bufferOriginFileOffset past valueStart when tracking a value
  // that spans multiple buffer fills — the opening bytes must remain in the buffer.
  safeConsumed = valueStart >= 0
    ? (int)Math.Min(reader.BytesConsumed, valueStart - bufferOriginFileOffset)
    : (int)reader.BytesConsumed

  bufferOriginFileOffset += safeConsumed
  remainingLen = dataEnd - safeConsumed
  Buffer.BlockCopy(buffer, safeConsumed, buffer, 0, remainingLen)

done:
  return result.AsReadOnly()

finally:
  ArrayPool.Return(buffer)

RecordEntry(key, buffer, start, end):
  length = (int)(end - start)
  copy = new byte[length]
  Buffer.BlockCopy(buffer, (int)(start - bufferOriginFileOffset), copy, 0, length)
  mem = new ReadOnlyMemory<byte>(copy)
  if keyIndex.TryGetValue(key, out idx):
    result[idx] = (key, mem)   // last-wins: overwrite value, preserve first-occurrence order
  else:
    keyIndex[key] = result.Count
    result.Add((key, mem))
```

### 3.3 Buffer Growth

| Condition | Action |
|-----------|--------|
| `remainingLen < buffer.Length` | Normal: refill spare space |
| `remainingLen == buffer.Length && buffer.Length < MaxBufferSize` | Double buffer size |
| `remainingLen == buffer.Length && buffer.Length == MaxBufferSize` | `NotSupportedException` |

16 MB accommodates approximately 5.5 million UTF-8 characters — more than 30 novels in
Japanese — which is sufficient for all practical single-value strings.

---

## 4. Edge Cases

| Case | Handling |
|------|----------|
| Empty object `{}` | Returns empty list |
| Object with a single key | Normal flow; one entry in result |
| Nested objects / arrays as values | Depth tracking captures full byte range; tokens read one by one until depth returns to 1 |
| Value spans buffer boundary | `Read()` returns `false`; `JsonReaderState` + `valueStart` persist; `safeConsumed` keeps value bytes in buffer; buffer grows as needed |
| String value > 16 MB | `NotSupportedException` thrown |
| Duplicate keys | Last value wins (consistent with `JSON.parse`); key order reflects first occurrence; implemented via `Dictionary<string, int>` index map |
| Cancellation | `OperationCanceledException` propagates; `ArrayPool.Return` in `finally` |
| Root token is not `{` (e.g., `[…]`, `42`) | All depth-1 tokens skipped (`currentKey` is always `null`); returns empty list without throwing |

---

## 5. Files to Create / Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/Engine/IO/JsonObject/TopLevelScanner.cs` | Create | Streaming scanner; returns key-value list |
| `tests/DataMorph.Tests/Engine/IO/JsonObject/TopLevelScannerTests.cs` | Create | Unit tests (shared fixtures) |
| `tests/DataMorph.Tests/Engine/IO/JsonObject/TopLevelScannerTests.Scan.cs` | Create | `Scan` correctness tests |
| `tests/DataMorph.Tests/Engine/IO/JsonObject/TopLevelScannerBenchmarks.cs` | Create | BenchmarkDotNet perf tests |

---

## 6. Testing Strategy

### 6.1 Unit Tests (`TopLevelScanner.Scan`)

| Test | Input | Expected |
|------|-------|----------|
| Empty object | `{}` | Empty list returned |
| Single primitive string | `{"k":"v"}` | `[("k", bytes of `"v"`)]` |
| Single primitive number | `{"n":42}` | `[("n", bytes of `42`)]` |
| Single nested object | `{"o":{"a":1}}` | `[("o", bytes of `{"a":1}`)]` |
| Single nested array | `{"a":[1,2]}` | `[("a", bytes of `[1,2]`)]` |
| Multiple keys — order preserved | `{"b":2,"a":1}` | `[("b",…),("a",…)]` in that order |
| Duplicate keys — last wins | `{"k":1,"k":2}` | `[("k", bytes of `2`)]` — last value; key order from first occurrence |
| Duplicate keys — triple occurrence | `{"k":1,"k":2,"k":3}` | `[("k", bytes of `3`)]` |
| Duplicate key — position stable with other keys | `{"a":1,"k":1,"b":2,"k":2}` | `[("a",…),("k", bytes of `2`),("b",…)]` — `k` stays at index 1 |
| Deeply nested value | `{"x":{"y":{"z":1}}}` | Correct byte range captured |
| Value spans buffer boundary | Object with value tokens crossing 1 MB boundary | Correct bytes captured; no exception |
| String value > 16 MB | Single string value exceeding MaxBufferSize | `NotSupportedException` thrown |
| Boolean true value | `{"k":true}` | `[("k", bytes of `true`)]` |
| Boolean false value | `{"k":false}` | `[("k", bytes of `false`)]` |
| Null value | `{"k":null}` | `[("k", bytes of `null`)]` |
| Cancellation | Any object | `OperationCanceledException` thrown; pool buffer returned |
| Leading/trailing whitespace | `  { "k" : 1 }  ` | Same result as compact form |

### 6.2 Benchmarks

```csharp
[MemoryDiagnoser]
class TopLevelScannerBenchmarks
```

| Benchmark | Description |
|-----------|-------------|
| `Scan_10Keys_SmallValues` | 10 keys with small primitive values |
| `Scan_100Keys_SmallValues` | 100 keys with small primitive values |
| `Scan_100Keys_LargeNestedValues` | 100 keys each holding a ~10 KB nested object |

Target: minimal heap allocations beyond the result `byte[]` copies.

---

## 7. Decision Record

### Why No `IRowIndexer`

`IRowIndexer` provides `GetCheckPoint(long targetRow)` for checkpoint-based random access used
by `SlidingWindowLruCache`. JSON Object files have no row concept and are opened once: after
the scan, all top-level values are in memory. A persistent indexer would add complexity with
no benefit.

### Why `IReadOnlyList` Over `IReadOnlyDictionary`

`IReadOnlyDictionary` was considered but rejected because it does not guarantee key insertion
order — display order would diverge from file order. `IReadOnlyList<(string key,
ReadOnlyMemory<byte> value)>` preserves first-occurrence order while the last-wins dedup
logic is handled internally by `keyIndex`.

### Why Eager Copy Over Lazy `(offset, length)`

Storing `(fileOffset, length)` and reading lazily on node expansion was considered but rejected:
- JSON Object files have few top-level keys; the memory savings are negligible
- Child-level expansion uses `ReadOnlyMemory<byte>.Slice` into already-copied bytes — no
  additional allocations

### Why Last-Wins for Duplicate Keys

RFC 8259 says keys within an object "SHOULD be unique" but does not forbid duplicates.
Last-wins (consistent with `JSON.parse`) was chosen because:
- Future table mode requires unique keys to identify actions unambiguously
- Preserving all duplicates would require callers to handle disambiguation
- Real-world JSON files with duplicate keys are vanishingly rare

Key order reflects first occurrence so that display order is stable and predictable.
Full duplicate-key support can be added later if a concrete use case arises.

### Why No `yield return` / Streaming

A streaming `IEnumerable<T>` approach was considered but rejected:
- Allocations (`string` keys, `byte[]` values) are identical regardless of enumeration style
- File open is a one-time, non-hot-path operation
- `Scan()` is a synchronous method; lazy enumeration would still complete the full scan
  before returning anything useful
