# PipelinesEngine Documentation

## Overview

`PipelinesEngine` is a high-performance streaming data processing component that bridges memory-mapped files with System.IO.Pipelines architecture. It enables zero-copy, efficient streaming of row-based data from large files without allocations.

## Key Features

- **Zero-Allocation Design**: Uses `System.IO.Pipelines` for minimal memory allocations
- **Integration with Existing Components**: Works seamlessly with `MmapService` and `RowIndexer`
- **Backpressure Support**: Built-in flow control prevents unbounded memory growth
- **Row-Based Streaming**: High-level API for streaming data row by row
- **Async/Await API**: Natural integration with `System.IO.Pipelines` asynchronous operations
- **Cancellation Support**: All write operations support `CancellationToken`

## Architecture

### Data Flow

```
File on Disk
    ↓
MmapService (Memory-Mapped File Access)
    ↓
RowIndexer (O(1) Row Offset Lookup)
    ↓
PipelinesEngine (Streaming Bridge)
    ↓
PipeWriter (Internal Buffer Management)
    ↓
PipeReader (Exposed for Consumption)
    ↓
Consumer (ViewManager, Parser, etc.)
    ↓
ReadOnlySequence<byte> (Zero-Copy Data Access)
```

### Component Integration

1. **MmapService**: Provides memory-mapped file access
2. **RowIndexer**: Provides byte offsets for each row
3. **PipelinesEngine**: Coordinates data flow from mmap to pipeline
4. **PipeReader**: Exposes buffered data to consumers

## API Reference

### Factory Method

```csharp
public static Result<PipelinesEngine> Create(
    MmapService mmapService,
    RowIndexer rowIndexer,
    PipeOptions? pipeOptions = null)
```

Creates a new `PipelinesEngine` instance.

**Parameters:**
- `mmapService`: The memory-mapped file service (required)
- `rowIndexer`: The row indexer for the file (required)
- `pipeOptions`: Optional pipe configuration (default: optimized for performance)

**Returns:** `Result<PipelinesEngine>` containing the engine instance or an error

**Throws:**
- `ArgumentNullException`: If `mmapService` or `rowIndexer` is null

### Properties

#### Reader

```csharp
public PipeReader Reader { get; }
```

Gets the `PipeReader` for consuming streamed data.

**Throws:** `ObjectDisposedException` if the engine has been disposed

#### RowCount

```csharp
public int RowCount { get; }
```

Gets the total number of rows available for streaming.

**Throws:** `ObjectDisposedException` if the engine has been disposed

### Methods

#### WriteRowAsync

```csharp
public async Task<Result> WriteRowAsync(int rowIndex, CancellationToken cancellationToken = default)
```

Writes a single row to the pipeline for streaming consumption.

**Parameters:**
- `rowIndex`: The zero-based row index
- `cancellationToken`: Optional cancellation token

**Returns:** `Task<Result>` indicating success or failure

**Throws:**
- `ObjectDisposedException`: If the engine has been disposed
- `OperationCanceledException`: If the operation was cancelled

#### WriteRowsAsync

```csharp
public async Task<Result> WriteRowsAsync(int startRow, int rowCount, CancellationToken cancellationToken = default)
```

Writes a range of rows to the pipeline for streaming consumption.

**Parameters:**
- `startRow`: The zero-based index of the first row to stream
- `rowCount`: The number of rows to stream
- `cancellationToken`: Optional cancellation token

**Returns:** `Task<Result>` indicating success or failure

**Remarks:**
- This method uses batch writing: all rows are written to the buffer and flushed once at the end for better performance
- The caller must read from `Reader` to avoid blocking due to backpressure
- Cancellation behavior:
  - Checked before processing each row (throws `OperationCanceledException`)
  - Checked during final flush (returns `Result.Failure`)

**Throws:**
- `ObjectDisposedException`: If the engine has been disposed
- `OperationCanceledException`: If the operation was cancelled during row processing

#### CompleteWriter

```csharp
public void CompleteWriter(Exception? exception = null)
```

Completes the PipeWriter, signaling no more data will be written.

**Parameters:**
- `exception`: Optional exception to signal error completion

This causes the `PipeReader` to return `IsCompleted = true` on subsequent reads.

#### Dispose

```csharp
public void Dispose()
```

Releases resources used by the engine. Safe to call multiple times.

## Usage Examples

### Basic Usage

```csharp
// Open file and build index
var mmapResult = MmapService.Open("data.csv");
if (mmapResult.IsFailure)
{
    Console.WriteLine($"Error: {mmapResult.Error}");
    return;
}

using var mmapService = mmapResult.Value;

var indexResult = RowIndexer.Build(mmapService);
if (indexResult.IsFailure)
{
    Console.WriteLine($"Error: {indexResult.Error}");
    return;
}

using var indexer = indexResult.Value;

// Create pipeline engine
var engineResult = PipelinesEngine.Create(mmapService, indexer);
if (engineResult.IsFailure)
{
    Console.WriteLine($"Error: {engineResult.Error}");
    return;
}

using var engine = engineResult.Value;

// Write all rows to pipeline
var writeResult = await engine.WriteRowsAsync(0, engine.RowCount);
if (writeResult.IsFailure)
{
    Console.WriteLine($"Error: {writeResult.Error}");
    return;
}

engine.CompleteWriter();

// Consume data from pipeline
await foreach (var line in ReadLinesAsync(engine.Reader))
{
    Console.WriteLine(line);
}

static async IAsyncEnumerable<string> ReadLinesAsync(PipeReader reader)
{
    while (true)
    {
        var result = await reader.ReadAsync();
        var buffer = result.Buffer;

        if (buffer.Length > 0)
        {
            string text;
            if (buffer.IsSingleSegment)
            {
                text = Encoding.UTF8.GetString(buffer.FirstSpan);
            }
            else
            {
                Span<byte> span = new byte[buffer.Length];
                var position = 0;
                foreach (var segment in buffer)
                {
                    segment.Span.CopyTo(span.Slice(position));
                    position += segment.Length;
                }
                text = Encoding.UTF8.GetString(span);
            }

            foreach (var line in text.Split('\n'))
            {
                yield return line;
            }
        }

        reader.AdvanceTo(buffer.End);

        if (result.IsCompleted)
        {
            break;
        }
    }
}
```

### Streaming Specific Row Range

```csharp
using var engine = PipelinesEngine.Create(mmapService, indexer).Value;

// Stream rows 100-199
var result = await engine.WriteRowsAsync(startRow: 100, rowCount: 100);
if (result.IsSuccess)
{
    engine.CompleteWriter();
    await ProcessDataAsync(engine.Reader);
}
```

### With Cancellation Support

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var engine = PipelinesEngine.Create(mmapService, indexer).Value;

try
{
    var result = await engine.WriteRowsAsync(0, engine.RowCount, cts.Token);
    if (result.IsSuccess)
    {
        engine.CompleteWriter();
        await ProcessDataAsync(engine.Reader, cts.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation cancelled after timeout");
}
```

### Custom Pipe Options

```csharp
var pipeOptions = new PipeOptions(
    pool: MemoryPool<byte>.Shared,
    pauseWriterThreshold: 1024 * 1024,  // 1MB threshold
    resumeWriterThreshold: 512 * 1024,  // 512KB resume
    minimumSegmentSize: 8192             // 8KB segments
);

using var engine = PipelinesEngine.Create(mmapService, indexer, pipeOptions).Value;
```

## Performance Characteristics

### Memory Usage

- **Buffer Pool**: Uses `MemoryPool<byte>.Shared` for buffer reuse
- **Default Thresholds**:
  - Pause Writer: 64KB (prevents unbounded growth)
  - Resume Writer: 32KB (hysteresis to prevent thrashing)
  - Minimum Segment: 4KB (balances fragmentation vs overhead)

### Backpressure

The pipeline automatically applies backpressure when the consumer falls behind:

1. Writer fills buffers up to `pauseWriterThreshold` (64KB default)
2. Further writes block until consumer reads data
3. Writing resumes when buffered data drops below `resumeWriterThreshold` (32KB default)

### Throughput

- **Row-Based Streaming**: Reads exact byte ranges per row (no chunking overhead)
- **Zero-Copy**: Data flows directly from memory-mapped file to pipeline buffers
- **SIMD-Accelerated Indexing**: Row offsets computed using RowIndexer's SIMD newline detection
- **Batch Writing**: `WriteRowsAsync` writes all rows to the buffer and flushes once at the end, reducing syscall overhead compared to per-row flushing

## Error Handling

### Result Pattern

All write operations return `Task<Result>` for expected failures:

```csharp
var result = await engine.WriteRowAsync(rowIndex);
if (result.IsFailure)
{
    Console.WriteLine($"Write failed: {result.Error}");
    return;
}

// Success case
result.OnSuccess(() => Console.WriteLine("Write succeeded"));
```

### Common Errors

| Error | Cause | Resolution |
|-------|-------|------------|
| "Row index X out of range" | Invalid row index provided | Check `engine.RowCount` before calling `WriteRowAsync` |
| "Row range exceeds total rows" | `startRow + rowCount` exceeds available rows | Validate range against `engine.RowCount` |
| "Failed to read row X" | MmapService read failure | Check file permissions and integrity |
| "Write operation was cancelled" | Flush cancelled by token | Handle cancellation gracefully |

### Exceptions

These exceptions are thrown (not returned in `Result`):

- `ArgumentNullException`: Null arguments to factory method
- `ObjectDisposedException`: Operations on disposed engine
- `OperationCanceledException`: Cancellation via `CancellationToken`

## Integration with MmapService and RowIndexer

### Initialization Sequence

```csharp
// 1. Open memory-mapped file
var mmapResult = MmapService.Open(filePath);
var mmapService = mmapResult.Value;

// 2. Build row index
var indexResult = RowIndexer.Build(mmapService);
var indexer = indexResult.Value;

// 3. Create pipeline engine
var engineResult = PipelinesEngine.Create(mmapService, indexer);
var engine = engineResult.Value;
```

### Resource Lifetime

- **MmapService**: Must remain alive while `PipelinesEngine` is in use
- **RowIndexer**: Must remain alive while `PipelinesEngine` is in use
- **PipelinesEngine**: Can be disposed independently; does NOT dispose dependencies

```csharp
using var mmapService = MmapService.Open(filePath).Value;
using var indexer = RowIndexer.Build(mmapService).Value;

// PipelinesEngine does not own mmapService or indexer
using var engine1 = PipelinesEngine.Create(mmapService, indexer).Value;
// ... use engine1 ...

// Can create another engine with same dependencies
using var engine2 = PipelinesEngine.Create(mmapService, indexer).Value;
// ... use engine2 ...
```

### Data Flow Details

When `WriteRowAsync(rowIndex)` is called:

1. `RowIndexer` provides byte offset: `startOffset = indexer[rowIndex]`
2. End offset calculated: `endOffset = indexer[rowIndex + 1]` (or file length for last row)
3. Bytes to read: `bytesToRead = endOffset - startOffset`
4. `PipeWriter.GetMemory(bytesToRead)` obtains buffer
5. `MmapService.TryRead(startOffset, buffer)` fills buffer
6. `PipeWriter.Advance(bytesToRead)` commits data
7. `PipeWriter.FlushAsync()` makes data available to `PipeReader`

## Design Decisions

### Why Async API?

- Natural integration with System.IO.Pipelines which is async-first
- Enables proper backpressure handling without blocking threads
- Better resource utilization for I/O-bound operations
- Follows modern .NET best practices for streaming data
- No need for ConfigureAwait(false) in application code (modern .NET has no synchronization context in CLI apps)

### Why Expose PipeReader?

- Follows System.IO.Pipelines best practices
- Enables zero-copy consumption via `ReadOnlySequence<byte>`
- Integrates with `Utf8JsonReader` which accepts `ReadOnlySequence<byte>`
- Gives consumers full control over buffer advancement

### Why Row-Based Instead of Byte-Based?

- Higher-level abstraction matches domain (rows in files)
- Integrates naturally with RowIndexer
- Easier for consumers (ViewManager works with row indices)
- Byte-based methods can be added if needed

## Thread Safety

`PipelinesEngine` is **not thread-safe**. Use from a single thread or apply external synchronization.

## Native AOT Compatibility

Fully compatible with Native AOT compilation:
- No reflection usage
- No dynamic code generation
- `System.IO.Pipelines` is part of .NET BCL

## Related Components

- **MmapService** (`src/Engine/IO/MmapService.cs`): Memory-mapped file access
- **RowIndexer** (`src/Engine/IO/RowIndexer.cs`): SIMD-accelerated row indexing
- **Result Pattern** (`src/Engine/Result.cs`): Error handling without exceptions

## See Also

- [MmapService Documentation](./mmap-service.md) (if exists)
- [RowIndexer Documentation](./row-indexer.md) (if exists)
- [System.IO.Pipelines Documentation](https://learn.microsoft.com/en-us/dotnet/standard/io/pipelines)
