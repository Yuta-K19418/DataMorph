using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace DataMorph.Engine.IO;

/// <summary>
/// Provides System.IO.Pipelines-based streaming data processing with zero-copy semantics.
/// </summary>
/// <remarks>
/// This engine bridges memory-mapped files with the Pipelines architecture, enabling
/// efficient streaming of row-based data without allocations. Uses ReadOnlySequence&lt;byte&gt;
/// for processing data spans that may cross buffer boundaries.
/// This class is not thread-safe and should be used from a single thread.
/// </remarks>
public sealed class PipelinesEngine : IDisposable
{
    private readonly MmapService _mmapService;
    private readonly RowIndexer _rowIndexer;
    private readonly Pipe _pipe;
    private bool _disposed;

    /// <summary>
    /// Gets the PipeReader for consuming streamed data.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The engine has been disposed.</exception>
    public PipeReader Reader
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _pipe.Reader;
        }
    }

    /// <summary>
    /// Gets the total number of rows available for streaming.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The engine has been disposed.</exception>
    public int RowCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _rowIndexer.RowCount;
        }
    }

    private PipelinesEngine(MmapService mmapService, RowIndexer rowIndexer, Pipe pipe)
    {
        _mmapService = mmapService;
        _rowIndexer = rowIndexer;
        _pipe = pipe;
    }

    /// <summary>
    /// Calculates the byte offset and length for a given row index.
    /// </summary>
    /// <param name="rowIndex">The zero-based row index.</param>
    /// <returns>A tuple containing the start offset and number of bytes to read.</returns>
    private (long startOffset, int bytesToRead) GetRowOffsetAndLength(int rowIndex)
    {
        var startOffset = _rowIndexer[rowIndex];
        // Calculate end offset: use next row's start, or file length for last row
        var endOffset = rowIndex + 1 < _rowIndexer.RowCount
            ? _rowIndexer[rowIndex + 1]  // Next row's start
            : _mmapService.Length;        // EOF for last row

        var bytesToRead = (int)(endOffset - startOffset);

        return (startOffset, bytesToRead);
    }

    /// <summary>
    /// Creates a PipelinesEngine from existing MmapService and RowIndexer.
    /// </summary>
    /// <param name="mmapService">The memory-mapped file service.</param>
    /// <param name="rowIndexer">The row indexer for the file.</param>
    /// <param name="pipeOptions">Optional pipe options for tuning buffer sizes.</param>
    /// <returns>A Result containing the PipelinesEngine on success, or an error on failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown if mmapService or rowIndexer is null.</exception>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "PipelinesEngine ownership is transferred to the caller via Result<T>")]
    public static Result<PipelinesEngine> Create(
        MmapService mmapService,
        RowIndexer rowIndexer,
        PipeOptions? pipeOptions = null)
    {
        ArgumentNullException.ThrowIfNull(mmapService);
        ArgumentNullException.ThrowIfNull(rowIndexer);

        var options = pipeOptions ?? new PipeOptions(
            pool: MemoryPool<byte>.Shared,
            readerScheduler: PipeScheduler.Inline,
            writerScheduler: PipeScheduler.Inline,
            pauseWriterThreshold: 65536,        // 64KB backpressure threshold
            resumeWriterThreshold: 32768,       // 32KB resume threshold
            minimumSegmentSize: 4096,           // 4KB minimum segments
            useSynchronizationContext: false
        );

        // Pipe ownership is transferred to PipelinesEngine.
        // The caller is responsible for disposing the engine, which will clean up the pipe.
        var pipe = new Pipe(options);
        var engine = new PipelinesEngine(mmapService, rowIndexer, pipe);

        return Results.Success(engine);
    }

    /// <summary>
    /// Writes a single row to the pipeline for streaming consumption.
    /// </summary>
    /// <param name="rowIndex">The zero-based row index.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    /// <exception cref="ObjectDisposedException">The engine has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
    public async Task<Result> WriteRowAsync(int rowIndex, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (rowIndex < 0 || rowIndex >= _rowIndexer.RowCount)
        {
            return Results.Failure($"Row index {rowIndex} out of range [0, {_rowIndexer.RowCount})");
        }

        var (startOffset, bytesToRead) = GetRowOffsetAndLength(rowIndex);

        var memory = _pipe.Writer.GetMemory(bytesToRead);
        var (success, error) = _mmapService.TryRead(startOffset, memory.Span[..bytesToRead]);

        if (!success)
        {
            return Results.Failure($"Failed to read row {rowIndex}: {error}");
        }

        _pipe.Writer.Advance(bytesToRead);

        // Flush to make data available to reader
        var flushResult = await _pipe.Writer.FlushAsync(cancellationToken);

        if (flushResult.IsCanceled)
        {
            return Results.Failure("Write operation was cancelled");
        }

        return Results.Success();
    }

    /// <summary>
    /// Writes a range of rows to the pipeline for streaming consumption.
    /// </summary>
    /// <param name="startRow">The zero-based index of the first row to stream.</param>
    /// <param name="rowCount">The number of rows to stream.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    /// <remarks>
    /// This method writes all rows in a batch and flushes once at the end for better performance.
    /// The caller must read from Reader to avoid blocking due to backpressure.
    /// Cancellation is checked before processing each row and during the final flush operation.
    /// If cancelled during row processing, an OperationCanceledException is thrown.
    /// If cancelled during flush, a Result.Failure is returned.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The engine has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled during row processing.</exception>
    public async Task<Result> WriteRowsAsync(int startRow, int rowCount, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (startRow < 0)
        {
            return Results.Failure($"Start row {startRow} cannot be negative");
        }

        if (rowCount < 0)
        {
            return Results.Failure($"Row count {rowCount} cannot be negative");
        }

        if (rowCount == 0)
        {
            return Results.Success(); // Empty range is valid
        }

        if (startRow + rowCount > _rowIndexer.RowCount)
        {
            return Results.Failure($"Row range [{startRow}, {startRow + rowCount}) exceeds total rows ({_rowIndexer.RowCount})");
        }

        // Write all rows to the buffer without flushing (batch writing for performance)
        for (var i = 0; i < rowCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentRow = startRow + i;
            var (startOffset, bytesToRead) = GetRowOffsetAndLength(currentRow);

            var memory = _pipe.Writer.GetMemory(bytesToRead);
            var (success, error) = _mmapService.TryRead(startOffset, memory.Span[..bytesToRead]);

            if (!success)
            {
                return Results.Failure($"Failed to read row {currentRow}: {error}");
            }

            _pipe.Writer.Advance(bytesToRead);
        }

        // Flush once at the end
        var flushResult = await _pipe.Writer.FlushAsync(cancellationToken);

        if (flushResult.IsCanceled)
        {
            return Results.Failure("Write operation was cancelled");
        }

        return Results.Success();
    }

    /// <summary>
    /// Completes the PipeWriter, signaling no more data will be written.
    /// </summary>
    /// <param name="exception">Optional exception to signal error completion.</param>
    public void CompleteWriter(Exception? exception = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pipe.Writer.Complete(exception);
    }

    /// <summary>
    /// Releases resources used by the engine.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _pipe.Reader.Complete();
        _pipe.Writer.Complete();
        _disposed = true;
    }
}
