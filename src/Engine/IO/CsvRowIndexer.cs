using System.Buffers;

namespace DataMorph.Engine.IO;

/// <summary>
/// Indexes CSV rows for efficient random access.
/// Supports comma-delimited CSV with RFC 4180 quoted field handling.
/// </summary>
public sealed class CsvRowIndexer
{
    private readonly Lock _lock = new();
    private readonly string _filePath;
    private readonly List<long> _checkpoints = [0];

    private const int BufferSize = 1024 * 1024; // 1MB
    private const int CheckPointInterval = 1000;

    private static readonly SearchValues<byte> _newlineAndQuote = SearchValues.Create("\n\""u8);

    private long _totalRows;

    public CsvRowIndexer(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the total number of rows indexed in the CSV file.
    /// Updated periodically during BuildIndex() (every 1000 rows) and finalized upon completion.
    /// </summary>
    public long TotalRows => Interlocked.Read(ref _totalRows);

    /// <summary>
    /// Builds the row index by scanning the entire CSV file.
    /// This method is NOT thread-safe and must be called once before any GetCheckPoint() calls.
    /// TotalRows is updated periodically during execution (every 1000 rows) for progress tracking.
    /// GetCheckPoint() can be safely called from other threads while BuildIndex() is running,
    /// but will return results based on the partially-constructed index.
    /// </summary>
    public void BuildIndex()
    {
        using var handle = File.OpenHandle(
            _filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read
        );
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            var fileOffset = 0L;
            var rowCount = 0L;
            var inQuotes = false;
            var lastByteRead = (byte)0;
            var totalBytesRead = 0L;

            while (true)
            {
                var bytesRead = RandomAccess.Read(handle, buffer.AsSpan(0, BufferSize), fileOffset);

                if (bytesRead <= 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
                lastByteRead = buffer[bytesRead - 1];

                var span = buffer.AsSpan(0, bytesRead);
                ProcessBuffer(span, ref fileOffset, ref rowCount, ref inQuotes);

                fileOffset += bytesRead;
            }

            // If file doesn't end with newline, count the last line
            if (totalBytesRead > 0 && lastByteRead != (byte)'\n' && !inQuotes)
            {
                rowCount++;
            }

            Interlocked.Exchange(ref _totalRows, rowCount);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Gets the nearest checkpoint for random access to a target row.
    /// Returns the file byte offset and row offset from that checkpoint.
    /// This method is thread-safe.
    /// </summary>
    /// <param name="targetRow">The zero-based row index to seek to.</param>
    /// <returns>A tuple of (byteOffset, rowOffset) where byteOffset is the file position in bytes and rowOffset is rows to advance from the checkpoint.</returns>
    public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow)
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
        ref bool inQuotes
    )
    {
        var position = 0;

        while (position < buffer.Length)
        {
            var searchStart = buffer[position..];
            var foundIndex = searchStart.IndexOfAny(_newlineAndQuote);

            if (foundIndex == -1)
            {
                break;
            }

            var absoluteIndex = position + foundIndex;
            var currentByte = buffer[absoluteIndex];

            position = absoluteIndex + 1;

            // Toggle quote state
            if (currentByte == (byte)'"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!(currentByte == (byte)'\n' && !inQuotes))
            {
                continue;
            }

            // Found newline outside quotes
            rowCount++;

            if (rowCount % CheckPointInterval != 0)
            {
                continue;
            }

            // Update _totalRows for progress tracking (every 1000 rows)
            Interlocked.Exchange(ref _totalRows, rowCount);

            // Store checkpoint every N rows
            var checkpointOffset = fileOffset + position;
            lock (_lock)
            {
                _checkpoints.Add(checkpointOffset);
            }
        }
    }
}
