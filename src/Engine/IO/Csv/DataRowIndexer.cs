using System.Buffers;

namespace DataMorph.Engine.IO.Csv;

/// <summary>
/// Indexes CSV data rows (excluding header) for efficient random access.
/// Supports comma-delimited CSV with RFC 4180 quoted field handling.
/// </summary>
public sealed class DataRowIndexer
{
    private readonly Lock _lock = new();
    private readonly string _filePath;

    /// <summary>
    /// Gets the path to the CSV file being indexed.
    /// </summary>
    public string FilePath => _filePath;
    private readonly List<long> _checkpoints = [];

    private const int BufferSize = 1024 * 1024; // 1MB
    private const int CheckPointInterval = 1000;

    private static readonly SearchValues<byte> _newlineAndQuote = SearchValues.Create("\n\""u8);

    private long _totalRows;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataRowIndexer"/> class.
    /// </summary>
    /// <param name="filePath">The path to the CSV file to index.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="filePath"/> is null or whitespace.</exception>
    public DataRowIndexer(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Gets the total number of data rows indexed in the CSV file (excluding header).
    /// Updated periodically during BuildIndex() (every 1000 rows) and finalized upon completion.
    /// </summary>
    public long TotalRows => Interlocked.Read(ref _totalRows);

    /// <summary>
    /// Builds the data row index by scanning the entire CSV file (header is skipped).
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
            var headerSkipped = false;

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
                ProcessBuffer(span, ref fileOffset, ref rowCount, ref inQuotes, ref headerSkipped);

                fileOffset += bytesRead;
            }

            // If file doesn't end with newline, count the last line (skip header)
            if (totalBytesRead > 0 && lastByteRead != (byte)'\n' && !inQuotes && headerSkipped)
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
            if (_checkpoints.Count == 0)
            {
                // No checkpoints available yet (before BuildIndex or empty file)
                return (-1, 0);
            }

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
        ref bool inQuotes,
        ref bool headerSkipped
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

            if (currentByte == (byte)'\n' && !headerSkipped)
            {
                headerSkipped = true;
                // Add first checkpoint after header
                var headerCheckpointOffset = fileOffset + position;
                _checkpoints.Add(headerCheckpointOffset);
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
