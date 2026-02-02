# IncrementalSchemaScanner Design Document

## Overview

This document describes the design for integrating CSV schema inference with `MainWindow.cs`. The `IncrementalSchemaScanner` class performs initial schema inference on the first 200 rows synchronously, then continues refining the schema in the background for the remaining rows.

## Architecture

```
CSV File Load (ShowFileDialogAsync)
       │
       ├─(1)─→ Create CsvDataRowIndexer (existing, for display only)
       │            └─→ Task.Run(BuildIndex)
       │
       ├─(2)─→ Create IncrementalSchemaScanner (NEW)
       │            │
       │            ├─(2a)─→ Read first 200 rows via Sep
       │            │            └─→ ScanSchema(first 200 rows)
       │            │
       │            └─(2b)─→ Start background scan via Sep
       │                         └─→ Skip rows 1-200
       │                         └─→ RefineSchema(row 201 onwards)
       │
       └─(3)─→ Create TableView with VirtualTableSource (unchanged)
```

## Design Decisions

### CsvDataRowIndexer Not Used for Schema Inference

**Reason**: `CsvDataRowIndexer` builds its index asynchronously, which would add coordination complexity if used for schema inference.

**Solution**: `IncrementalSchemaScanner` reads the CSV file directly using Sep (nietras.SeparatedValues), independent of the indexer.

**Benefits**:
- Simpler design with no dependency on indexer state
- Schema inference can start immediately without waiting for index construction
- Clear separation of concerns: indexer for random access, schema updater for type inference

### Zero-Allocation Type Inference

The design prioritizes memory efficiency in hot paths:

- Use Sep's `ReadOnlySpan<char>` based APIs for direct type inference
- `CsvTypeInferrer.InferType(ReadOnlySpan<char>)` operates on spans without string allocation
- Copy-on-Write pattern ensures zero allocations when schema is stable

### Thread-Safe Schema Updates

The schema reference is updated atomically using `volatile` and `Interlocked.Exchange`:

```csharp
private volatile TableSchema _schema;

private void UpdateSchema(TableSchema newSchema)
{
    if (!ReferenceEquals(_schema, newSchema))
    {
        Interlocked.Exchange(ref _schema, newSchema);
    }
}
```

This ensures:
- Readers always see a consistent schema reference
- No locks required for read operations
- Safe concurrent access from UI thread and background scan

## Implementation Details

### IncrementalSchemaScanner Class

**Location**: `src/App/Schema/IncrementalSchemaScanner.cs`

**Responsibilities**:
- Read first 200 rows and perform initial schema inference
- Continue scanning remaining rows in background
- Provide thread-safe access to current schema

**Class Structure**:

```csharp
namespace DataMorph.App.Schema;

internal sealed class IncrementalSchemaScanner : IDisposable
{
    private const int InitialScanCount = 200;

    private volatile TableSchema _schema;
    private readonly string _filePath;
    private readonly string[] _columnNames;
    private CancellationTokenSource? _backgroundScanCts;

    /// <summary>
    /// Gets the current schema. Thread-safe for concurrent reads.
    /// </summary>
    public TableSchema Schema => _schema;

    public IncrementalSchemaScanner(
        string filePath,
        string[] columnNames,
        TableSchema initialSchema)
    {
        _filePath = filePath;
        _columnNames = columnNames;
        _schema = initialSchema;
    }

    /// <summary>
    /// Performs initial scan on first 200 rows synchronously.
    /// Must be awaited before UI can display schema.
    /// </summary>
    public Task InitialScanAsync();

    /// <summary>
    /// Starts background scan from row 201 onwards.
    /// Fire-and-forget - returns Task but should not be awaited.
    /// </summary>
    public Task StartBackgroundScanAsync();

    public void Dispose();
}
```

### InitialScanAsync Method

```csharp
public async Task InitialScanAsync()
{
    await Task.Run(() =>
    {
        var rows = ReadRows(0, InitialScanCount);
        if (rows.Count == 0)
        {
            return;
        }

        var scanResult = CsvSchemaScanner.ScanSchema(
            _columnNames,
            rows,
            InitialScanCount);

        if (scanResult.IsSuccess)
        {
            UpdateSchema(scanResult.Value);
        }
    });
}
```

### StartBackgroundScanAsync Method

```csharp
public Task StartBackgroundScanAsync()
{
    _backgroundScanCts = new CancellationTokenSource();
    var token = _backgroundScanCts.Token;

    return Task.Run(() =>
    {
        try
        {
            ProcessRemainingRows(token);
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed
        }
    }, token);
}

private void ProcessRemainingRows(CancellationToken token)
{
    const int batchSize = 1000;
    var rowIndex = InitialScanCount;

    while (!token.IsCancellationRequested)
    {
        var rows = ReadRows(rowIndex, batchSize);
        if (rows.Count == 0)
        {
            break;
        }

        foreach (var row in rows)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            var refineResult = CsvSchemaScanner.RefineSchema(_schema, row);
            if (refineResult.IsSuccess)
            {
                UpdateSchema(refineResult.Value);
            }
        }

        rowIndex += rows.Count;
    }
}
```

### ReadRows Helper

Reads rows directly using Sep, bypassing `CsvDataRowIndexer`:

```csharp
private IReadOnlyList<CsvDataRow> ReadRows(int startRow, int count)
{
    var rows = new List<CsvDataRow>(count);

    using var reader = Sep.New(',').Reader().FromFile(_filePath);

    // Skip to start row
    var currentRow = 0;
    while (currentRow < startRow && reader.MoveNext())
    {
        currentRow++;
    }

    // Read requested rows
    var readCount = 0;
    while (readCount < count && reader.MoveNext())
    {
        var record = reader.Current;
        var columns = new ReadOnlyMemory<char>[_columnNames.Length];

        // ... copy column data to ReadOnlyMemory<char> ...

        rows.Add(columns);
        readCount++;
    }

    return rows;
}
```

### MainWindow.ShowFileDialog Changes

**File**: `src/App/MainWindow.cs`

The synchronous `ShowFileDialog()` method becomes asynchronous `ShowFileDialogAsync()`:

```csharp
private async Task ShowFileDialogAsync()
{
    var dialog = new OpenDialog { Title = "Open File" };
    dialog.AllowedTypes.Add(new AllowedType("CSV file", ".csv"));
    dialog.AllowedTypes.Add(new AllowedType("JSON file", ".json"));

    _app.Run(dialog);

    if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
    {
        return;
    }

    _state.CurrentFilePath = dialog.Path;

    if (dialog.Path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
    {
        // 1. Create indexer for display (existing)
        var indexer = new CsvDataRowIndexer(dialog.Path);
        _ = Task.Run(indexer.BuildIndex);

        // 2. Create header-only schema for column names (existing)
        var headerResult = CsvSchemaCreator.CreateSchemaFromCsvHeader(dialog.Path);
        if (headerResult.IsFailure)
        {
            ShowError(headerResult.Error);
            return;
        }

        // 3. Create IncrementalSchemaScanner (NEW)
        var schemaScanner = new IncrementalSchemaScanner(
            dialog.Path,
            headerResult.Value.Columns.Select(c => c.Name).ToArray(),
            headerResult.Value);

        // 4. Perform initial scan (NEW) - blocks until 200 rows scanned
        await schemaScanner.InitialScanAsync();

        // 5. Update state with inferred schema (NEW)
        _state.Schema = schemaScanner.Schema;
        _state.SchemaScanner = schemaScanner;

        // 6. Start background scan (NEW) - fire-and-forget
        _ = schemaScanner.StartBackgroundScanAsync();

        // 7. Create TableView (indexer for display, schema from scanner)
        SwitchToTableView(indexer, schemaScanner.Schema);
        return;
    }

    // JSON handling unchanged...
}
```

### AppState Changes

**File**: `src/App/AppState.cs`

Add property to hold the schema updater:

```csharp
/// <summary>
/// Gets or sets the incremental schema scanner for background schema refinement.
/// Null if no CSV file is loaded.
/// </summary>
public IncrementalSchemaScanner? SchemaScanner { get; set; }
```

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Initial scan fails | Fall back to header-only schema (all `ColumnType.Text`) |
| Background scan fails | Log error and continue; schema remains at last valid state |
| Individual row parse error | Skip row, continue with next |
| File I/O error during background scan | Stop background scan gracefully |

## Thread Safety Summary

| Operation | Thread Safety |
|-----------|---------------|
| `Schema` property read | Safe (volatile reference) |
| `UpdateSchema()` | Safe (Interlocked.Exchange) |
| `InitialScanAsync()` | Must complete before UI uses schema |
| `StartBackgroundScanAsync()` | Runs independently, updates atomic |
| `Dispose()` | Cancels background task safely |

## Files to Modify/Create

| File | Action | Description |
|------|--------|-------------|
| `src/App/Schema/IncrementalSchemaScanner.cs` | **Create** | New class for incremental schema scanning |
| `src/App/MainWindow.cs` | **Modify** | Convert `ShowFileDialog` to async, integrate `IncrementalSchemaScanner` |
| `src/App/AppState.cs` | **Modify** | Add `SchemaScanner` property |
| `tests/DataMorph.Tests/App/Schema/IncrementalSchemaScannerTests.cs` | **Create** | Unit tests for `IncrementalSchemaScanner` |

## Potential Concerns and Mitigations

### 1. File Read Twice

**Concern**: Both `CsvDataRowCache` (for display) and `IncrementalSchemaScanner` (for schema inference) read the file independently.

**Mitigation**: Sep buffers efficiently, and modern OS file caching handles repeated reads. This is acceptable for typical CSV viewer usage.

### 2. File Modification During Processing

**Concern**: If the file is modified while being processed, display and schema could become inconsistent.

**Mitigation**: This is acceptable for a typical CSV viewer. Users are expected to reload modified files.

### 3. Large File Memory Usage

**Concern**: Reading 200 rows for initial scan allocates memory.

**Mitigation**: Initial scan is bounded (200 rows max). Background scan processes in batches and does not accumulate rows in memory.

### 4. Menu Action Becomes Async

**Concern**: `ShowFileDialog` action needs to be async, but Terminal.Gui menu items expect synchronous actions.

**Mitigation**: Use `async void` pattern for the event handler, or wrap in `Task.Run`. This is standard practice for UI event handlers.

## Test Strategy

### Unit Tests for IncrementalSchemaScanner

1. **InitialScanAsync_InfersTypesFromFirst200Rows**
   - Create test CSV with typed data
   - Verify schema types after initial scan

2. **BackgroundScan_RefinesSchema**
   - Create CSV where row 201+ has new nullable values
   - Verify schema updates after background scan completes

3. **Dispose_CancelsBackgroundScan**
   - Start background scan on large file
   - Dispose immediately
   - Verify no exceptions and graceful cancellation

4. **SchemaProperty_IsThreadSafe**
   - Read schema from multiple threads during update
   - Verify no corruption or exceptions

### Integration Tests

1. **MainWindow_LoadCsv_InfersSchema**
   - Load test CSV via MainWindow
   - Verify `AppState.Schema` contains inferred types

## Summary

The `IncrementalSchemaScanner` provides:

1. **Fast initial display**: 200-row scan completes quickly, enabling immediate UI display
2. **Accurate schema**: Background scan refines types from full file
3. **Thread safety**: Copy-on-Write + volatile ensures safe concurrent access
4. **Clean separation**: Schema inference independent of display indexing
5. **Resource management**: Proper cancellation and disposal
