# Design: JSON Lines Cell Value Zero-Allocation

## In Scope

- Replace the per-cell heap-allocated `string` in
  `JsonLinesRecordReader.GetCellData`/`ReadPropertyValue` (Number, Object,
  Array, String branches) with an `ArrayPool<char>`-backed buffer reused
  across calls, using `Encoding.UTF8.GetChars` (Number/Object/Array) and
  `Utf8JsonReader.CopyString` (String).
- Buffer lifecycle management: lazy rent on first use, grow-and-return-old
  on overflow (initial 256 chars, next power-of-two growth thereafter), and
  `Return` in `Dispose()`.
- Formalize the resulting "`CellData.Value` is valid only until the next
  `GetCellData` call" contract in XML documentation (`CellData.cs` and/or
  `IRecordReader.GetCellData`). Existing call sites
  (`RecordProcessor`, `JsonLinesRecordWriter`, `CsvRecordWriter`) already
  consume the value synchronously and immediately, so this is a
  documentation change backed by an already-verified invariant, not a
  behavior change to those call sites.
- Unit tests covering buffer reuse across consecutive cells in the same
  row, growth beyond the initial buffer size, escape-sequence resolution,
  and the empty-string boundary.
- A `BenchmarkDotNet` benchmark (`[MemoryDiagnoser]`), following the
  existing `JsonObjectCellExtractorBenchmarks.cs` pattern, to measure the
  resulting allocation count for `GetCellData`.

## Out of Scope

- **`CsvRecordReader`/`CsvRecordWriter`**: unchanged. Sep already returns
  `ReadOnlySpan<char>` directly with no per-cell allocation; the issue this
  design addresses is JSON Lines–specific.
- **`JsonLinesRecordWriter`**: unchanged. It only consumes `CellData.Value`
  and has no reason to distinguish a pooled-buffer-backed span from a
  `string`-backed one.
- **`Boolean`/`Null`/`Missing`/`Invalid` branches of `GetCellData`**:
  unchanged. These already carry no per-cell allocation today.
- **`JsonObjectCellExtractor`/`JsonByteExtractor`** (shared Engine-layer
  code also used by the TUI table/tree views): unchanged.
  `JsonByteExtractor.ExtractValueBytes` was confirmed to already be
  allocation-free (it only slices `ReadOnlyMemory<byte>`), so it needs no
  changes for this design to reach zero allocation. The known, separately
  tracked TUI-side bugs in `FormatValue`/`FormatNumber` are not addressed
  here.
- **`RecordProcessor`**: unchanged. `CellData`'s shape is not changing, so
  no caller-side code needs to change.
- **`ColumnType`/`TypeInferrer`**: unrelated, unchanged.
- **`IRecordReader`/`IRecordWriter` interface signatures**: unchanged.
  `GetCellData` still returns `CellData`; only the implementing struct's
  `readonly` modifier changes.
- **A byte-native JSON Lines → JSON Lines pipeline** (Alternative C in
  `docs/design_batch_cell_typed_channel.md`): still not pursued.
  `ArrayPool<char>` pooling alone reaches zero allocation without the added
  complexity of a second, format-pair-specific pipeline.
- **Wiring the new benchmark into CI**: it is a manually-run diagnostic,
  matching every other `BenchmarkDotNet` class already in this repository.

---

## Files Changed

| File | Change |
|------|--------|
| `src/App/Cli/JsonLinesRecordReader.cs` | Main change — see "Implementation Approach" below |
| `src/App/Cli/CellData.cs` | XML doc update only: document that `Value` is valid only until the next `GetCellData` call |
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordReaderTests.cs` | Add cases for buffer reuse across cells, growth beyond the initial size, escape-sequence resolution, and the empty-string boundary |
| `tests/Refedle.Tests/App/Cli/JsonLinesRecordReaderBenchmarks.cs` | **New.** `[MemoryDiagnoser]` benchmark for `GetCellData`, following the `JsonObjectCellExtractorBenchmarks.cs` pattern |

No changes to `CsvRecordReader.cs`, `CsvRecordWriter.cs`, `JsonLinesRecordWriter.cs`, `IRecordReader.cs`, `IRecordWriter.cs`, `RecordProcessor.cs`, `JsonByteExtractor.cs`, `JsonObjectCellExtractor.cs`, `ColumnType.cs`, or `TypeInferrer.cs` — see "Out of Scope" above.

---

## Implementation Approach

### 1. New field and buffer helper

```csharp
private const int MinimumBufferSize = 256;

private char[]? _valueBuffer;

private void EnsureBufferCapacity(int requiredLength)
{
    if (_valueBuffer is not null && _valueBuffer.Length >= requiredLength)
    {
        return;
    }

    if (_valueBuffer is not null)
    {
        ArrayPool<char>.Shared.Return(_valueBuffer);
    }

    var newSize = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(MinimumBufferSize, requiredLength));
    _valueBuffer = ArrayPool<char>.Shared.Rent(newSize);
}
```

`requiredLength` is always sized as an upper bound on the resulting char
count *before* the write happens (UTF-8 byte count for Number/Object/Array,
raw escaped byte count for String — see step 2), so the underlying
`Encoding.UTF8.GetChars`/`Utf8JsonReader.CopyString` call never hits its
buffer-too-small path. `EnsureBufferCapacity` is therefore the only place
buffer sizing decisions are made; no exception-driven retry is needed
(consistent with the project's "no exceptions for flow control" rule).

### 2. Per-token-type extraction helpers, replacing `ReadPropertyValue`'s inline expressions

`ReadPropertyValue` changes from `private static` to a `private` instance
method (still taking `Utf8JsonReader reader` by value — the existing
comment explaining that choice, and the `S1541`/`CS8168`/`CS8347` reasoning
behind it, remains valid and unchanged; only the enclosing method stops
being `static` so it can reach `_valueBuffer` through `this`). Its `Number`
and `StartObject`/`StartArray`/`String` arms are extracted into three small
instance helpers so the switch expression stays a single dispatch and each
helper stays under the nesting/complexity limits:

```csharp
private CellData NumberToCellData(Utf8JsonReader reader)
{
    var bytes = reader.ValueSpan;
    EnsureBufferCapacity(bytes.Length);
    var charsWritten = Encoding.UTF8.GetChars(bytes, _valueBuffer);
    return new CellData(_valueBuffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.Raw);
}

private CellData ObjectOrArrayToCellData(Utf8JsonReader reader, JsonRawBytes containingBytes)
{
    var bytes = JsonByteExtractor.ExtractValueBytes(ref reader, containingBytes).Span;
    EnsureBufferCapacity(bytes.Length);
    var charsWritten = Encoding.UTF8.GetChars(bytes, _valueBuffer);
    return new CellData(_valueBuffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.Raw);
}

private CellData StringToCellData(Utf8JsonReader reader)
{
    EnsureBufferCapacity(reader.ValueSpan.Length);
    var charsWritten = reader.CopyString(_valueBuffer);
    return new CellData(_valueBuffer.AsSpan(0, charsWritten), CellPresence.Value, CellEncoding.PlainText);
}
```

`reader.ValueSpan.Length` (raw UTF-8 byte count) is a safe upper bound for
the decoded char count in both cases: a multi-byte UTF-8 sequence always
decodes to fewer chars than its byte count, and every JSON escape sequence
(`\n`, `\uXXXX`, …) occupies more source bytes than the character(s) it
resolves to. `ReadPropertyValue`'s switch expression then becomes:

```csharp
private CellData ReadPropertyValue(Utf8JsonReader reader, JsonRawBytes containingBytes)
{
    return reader.TokenType switch
    {
        JsonTokenType.Null => new CellData([], CellPresence.Null),
        JsonTokenType.Number => NumberToCellData(reader),
        JsonTokenType.StartObject or JsonTokenType.StartArray => ObjectOrArrayToCellData(reader, containingBytes),
        JsonTokenType.String => StringToCellData(reader),
        JsonTokenType.True => new CellData("true", CellPresence.Value, CellEncoding.Boolean),
        JsonTokenType.False => new CellData("false", CellPresence.Value, CellEncoding.Boolean),
        _ => new CellData([], CellPresence.Invalid),
    };
}
```

### 3. `GetCellData` loses its `readonly` modifier

Renting/re-renting `_valueBuffer` mutates struct state, so
`public readonly CellData GetCellData(...)` becomes
`public CellData GetCellData(...)`. `ThrowIfDisposed` and `EvaluateFilters`
are unaffected and stay `readonly`.

### 4. `Dispose()`

```csharp
public void Dispose()
{
    if (_disposed)
    {
        return;
    }

    _rowReader?.Dispose();
    _rowReader = null;

    if (_valueBuffer is not null)
    {
        ArrayPool<char>.Shared.Return(_valueBuffer);
        _valueBuffer = null;
    }

    _disposed = true;
}
```

### 5. `CellData.cs` documentation

Add an XML doc remark on `CellData.Value` (or on `IRecordReader.GetCellData`)
stating that the returned span is valid only until the reader's next
`GetCellData` call — the buffer backing it is reused, not
freshly-allocated per cell.

---

## Decision Record

### Rationale

**`ArrayPool<char>` buffer reuse, not a per-cell `string`:** This is
exactly the performance investment `docs/design_batch_cell_typed_channel.md`
deferred ("Alternative D" in its Decision Record) rather than bundling into
a correctness fix. That design's stated blockers — buffer lifecycle
management, the "valid only until next call" constraint, and re-rent
handling on overflow — are what this design resolves.

**`Utf8JsonReader.CopyString`, not a hand-rolled unescape:** `CopyString`
is the standard, escape-resolving API for copying a JSON string's decoded
characters into a caller-owned buffer without allocating (confirmed via a
scratch program: it returns the number of characters written as `int`, and
throws `ArgumentException` if the destination is too small). Re-implementing
JSON string unescaping by hand would be strictly worse: more code, and a new
opportunity to diverge from `Utf8JsonReader`'s own (already-correct)
unescaping behavior.

**Pre-sized buffers, not catch-and-retry on `ArgumentException`:** Both
`Encoding.UTF8.GetChars` and `Utf8JsonReader.CopyString` throw when the
destination span is too small rather than reporting a required size. Sizing
the buffer to an upper bound (`reader.ValueSpan.Length`, confirmed always
`>=` the eventual char count for both UTF-8 decoding and JSON-escape
resolution) before writing avoids ever depending on that exception path,
consistent with the project's rule against using exceptions for flow
control.

**`ReadPropertyValue` becomes an instance method:** it needs `this` to
reach `_valueBuffer`/`EnsureBufferCapacity`. The existing comment on this
method explains why `Utf8JsonReader` is taken *by value* rather than `ref`
(a `ref Utf8JsonReader` parameter makes the ref struct return value
ref-safety-inferred as escaping through that parameter, which fails to
compile — `CS8168`/`CS8347`). That reasoning is about the parameter's own
by-value-vs-by-ref shape, not about whether the method is `static`, so it is
unaffected by this change and the comment does not need to be rewritten.

**Splitting `ReadPropertyValue`'s switch arms into three helper methods:**
each arm now needs two statements (size the buffer, then decode) instead of
one expression, which no longer fits cleanly inside a switch *expression*
arm. Extracting `NumberToCellData`/`ObjectOrArrayToCellData`/`StringToCellData`
keeps the switch itself a flat, single-expression dispatch (unchanged
structure from today) rather than converting it to a switch *statement*
with a nested body per branch.

### Consequences

- `GetCellData`'s output is unchanged for every existing test case; only
  the backing storage of `CellData.Value` changes (pooled buffer instead of
  an independently-GC-managed `string`).
- `CellData.Value`'s "valid only until the next `GetCellData` call"
  constraint moves from an accepted-but-inert observation (in
  `docs/design_batch_cell_typed_channel.md`'s Consequences — each `string`
  was independently valid regardless of subsequent calls, so nothing could
  actually break it) to a load-bearing invariant: reusing the same buffer
  means a stale `CellData` handed to a caller that violates the contract
  would observe corrupted data. All current call sites were audited and
  consume the value synchronously before the next call, so this is not a
  behavior change today, but it is now something a future change to
  `RecordProcessor` or a writer must not break — hence documenting it
  explicitly (see "Implementation Approach", step 5).
- `JsonLinesRecordReader` remains a single-consumer, sequential-access
  struct, matching its existing contract; the pooled buffer does not change
  its thread-safety posture (it was never safe to share across concurrent
  calls).
- One rented `char[]` buffer lives for the lifetime of a `JsonLinesRecordReader`
  instance (from first non-`Boolean`/`Null` cell read until `Dispose()`),
  instead of a fresh `string` per cell being independently collected by the
  GC. This trades a bounded, reused allocation for an unbounded stream of
  small ones — the intended effect of this change.

### Test Plan

In addition to `JsonLinesRecordReaderTests.cs`'s existing per-token-type
coverage (unaffected — same expected `Value`/`Presence`/`Encoding` per
case), add:

- Reading two or more columns from the same row in sequence, asserting each
  `CellData.Value.ToString()` is correct at the point it's read — the
  actual `RecordProcessor` consumption pattern, exercised directly against
  buffer reuse.
- A String value long enough to force growth past the initial 256-char
  buffer, asserting the grown buffer still produces the correct value.
- A JSON string containing escape sequences (e.g. an embedded quote and a
  `\n`), asserting the resolved text, not the raw escaped source.
- An empty JSON string (`""`), asserting `EnsureBufferCapacity`'s
  `Math.Max(MinimumBufferSize, requiredLength)` floor is exercised without
  error.
- The new `JsonLinesRecordReaderBenchmarks.cs`: representative Number,
  String, Object, and Array columns benchmarked with `[MemoryDiagnoser]`,
  confirming `Allocated` is at (or near) zero for `GetCellData`.
