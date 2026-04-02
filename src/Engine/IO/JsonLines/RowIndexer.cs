using System.Buffers;

namespace DataMorph.Engine.IO.JsonLines;

/// <summary>
/// Indexes JSON Lines files by row position for efficient random access.
/// Each line in a JSON Lines file contains a complete, independent JSON object.
/// </summary>
public sealed class RowIndexer : RowIndexerBase
{
    private readonly Lock _lock = new();
    private readonly string _filePath;
    private readonly List<long> _checkpoints = [0];

    private long _totalRows;
    private long _bytesRead;


    private const int BufferSize = 1024 * 1024; // 1MB
    private const int CheckPointInterval = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="RowIndexer"/> class.
    /// </summary>
    /// <param name="filePath">The path to the JSON Lines file to index.</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    public RowIndexer(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the path to the JSON Lines file being indexed.
    /// </summary>
    public override string FilePath => _filePath;

    /// <summary>
    /// Gets the total number of rows indexed.
    /// Updated periodically during <see cref="BuildIndex"/> (every 1000 rows) and finalized upon completion.
    /// Thread-safe via Interlocked operations.
    /// </summary>
    public override long TotalRows => Interlocked.Read(ref _totalRows);

    /// <summary>
    /// Bytes read so far. Updated atomically after each buffer read.
    /// Safe to read from any thread.
    /// </summary>
    public override long BytesRead => Interlocked.Read(ref _bytesRead);

    /// <summary>
    /// Builds the row index by scanning the entire file.
    /// Exits cooperatively when <paramref name="ct"/> is cancelled.
    /// NOT thread-safe — call once from a single background thread.
    /// <see cref="RowIndexerBase.BuildIndexCompleted"/> fires unconditionally when this method
    /// returns, regardless of whether it completed, was cancelled, or threw.
    /// </summary>
    public override void BuildIndex(CancellationToken ct = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            FileSize = new FileInfo(_filePath).Length;

            using var handle = File.OpenHandle(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
            var scanner = new RowScanner();

            var fileOffset = 0L;
            var rowCount = 0L;
            var lastByteRead = (byte)0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var bytesRead = RandomAccess.Read(handle, buffer.AsSpan(0, BufferSize), fileOffset);

                if (bytesRead <= 0)
                {
                    break;
                }

                Interlocked.Add(ref _bytesRead, bytesRead);
                lastByteRead = buffer[bytesRead - 1];

                var span = buffer.AsSpan(0, bytesRead);
                ProcessBuffer(span, ref fileOffset, ref rowCount, ref scanner);
                fileOffset += bytesRead;
            }

            // If file doesn't end with newline, count the last line
            if (Interlocked.Read(ref _bytesRead) > 0 && lastByteRead != (byte)'\n')
            {
                rowCount++;
            }

            Interlocked.Exchange(ref _totalRows, rowCount);

            // Empty-file / sub-checkpoint guard: FirstCheckpointReached must
            // always fire so that the TaskCompletionSource in FileLoader does
            // not hang. Must fire AFTER TotalRows is finalised.
            OnFirstCheckpointReached();
        }
        finally
        {
            // Guarantee FirstCheckpointReached fires even on cancellation or error,
            // so the TaskCompletionSource in FileLoader never hangs.
            OnFirstCheckpointReached();

            ArrayPool<byte>.Shared.Return(buffer);
            OnBuildIndexCompleted();
        }
    }

    /// <summary>
    /// Gets the nearest checkpoint for random access to a target row.
    /// Returns the file byte offset and row offset from that checkpoint.
    /// This method is thread-safe.
    /// </summary>
    /// <param name="targetRow">The zero-based row index to seek to.</param>
    /// <returns>
    /// A tuple of (byteOffset, rowOffset) where byteOffset is the file position in bytes
    /// and rowOffset is the number of rows to advance from the checkpoint.
    /// </returns>
    public override (long byteOffset, int rowOffset) GetCheckPoint(long targetRow)
    {
        lock (_lock)
        {
            var idealCheckPointIndex = (int)(targetRow / CheckPointInterval);
            // Clamp to the last available checkpoint (handles partial indexing or beyond-EOF requests)
            var actualCheckPointIndex = Math.Min(idealCheckPointIndex, _checkpoints.Count - 1);
            var byteOffset = _checkpoints[actualCheckPointIndex];
            // Calculate the row number that the actual checkpoint represents
            var actualCheckPointRow = (long)actualCheckPointIndex * CheckPointInterval;
            // Calculate how many rows to skip from the actual checkpoint to reach the target row
            var rowOffset = (int)(targetRow - actualCheckPointRow);

            return (byteOffset, rowOffset);
        }
    }

    private void ProcessBuffer(
        ReadOnlySpan<byte> buffer,
        ref long fileOffset,
        ref long rowCount,
        ref RowScanner scanner
    )
    {
        var position = 0;

        while (position < buffer.Length)
        {
            var remainingSpan = buffer[position..];
            var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(remainingSpan);

            // bytesConsumed should always be > 0 when remainingSpan is not empty
            if (bytesConsumed <= 0)
            {
                throw new InvalidDataException(
                    $"FindNextLineLength returned non-positive bytesConsumed ({bytesConsumed}) "
                        + $"for a non-empty span (length={remainingSpan.Length}) at position={position}, "
                        + $"fileOffset={fileOffset}"
                );
            }

            // Always advance position by the number of bytes consumed
            position += bytesConsumed;

            if (lineCompleted)
            {
                // A complete line was found (ending with an unescaped newline)
                rowCount++;

                if (rowCount % CheckPointInterval == 0)
                {
                    // Update _totalRows for progress tracking (every 1000 rows)
                    Interlocked.Exchange(ref _totalRows, rowCount);

                    // Store checkpoint every N rows
                    var checkpointOffset = fileOffset + position;
                    lock (_lock)
                    {
                        _checkpoints.Add(checkpointOffset);
                    }

                    OnFirstCheckpointReached();

                    OnProgressChanged(Interlocked.Read(ref _bytesRead), FileSize);
                }
            }
        }
    }
}
