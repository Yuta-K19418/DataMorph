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
  changes for this design to reach zero allocation. The known TUI-side bugs
  in `FormatValue`/`FormatNumber` (#272/#273) are not addressed here.
- **`RecordProcessor`**: unchanged. `CellData`'s shape is not changing, so
  no caller-side code needs to change.
- **`ColumnType`/`TypeInferrer`**: unrelated, unchanged.
- **`IRecordReader`/`IRecordWriter` interface signatures**: unchanged.
  `GetCellData` still returns `CellData`; only the implementing struct's
  `readonly` modifier changes.
- **A byte-native JSON Lines → JSON Lines pipeline** (Alternative C in the
  #267 design doc, `docs/design_batch_cell_typed_channel.md`): still not
  pursued. `ArrayPool<char>` pooling alone reaches zero allocation without
  the added complexity of a second, format-pair-specific pipeline.
- **Wiring the new benchmark into CI**: it is a manually-run diagnostic,
  matching every other `BenchmarkDotNet` class already in this repository.
