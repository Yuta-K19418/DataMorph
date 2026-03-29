using System.Buffers;

namespace DataMorph.Engine.IO.Csv;

/// <summary>
/// Indexes CSV data rows (excluding header) for efficient random access.
/// Supports comma-delimited CSV with RFC 4180 quoted field handling.
/// </summary>
/// <remarks>
/// <para><b>Event Design: Action vs EventHandler</b></para>
/// <para>This class uses Action delegates for events instead of the .NET-recommended EventHandler pattern.
/// This is an intentional design decision justified below:</para>
///
/// <para><b>Action<c>&lt;T&gt;</c> / Action Pros</b></para>
/// <list type="bullet">
///   <item>Simple: no unnecessary 'this' (sender) argument</item>
///   <item>Zero-allocation: no EventArgs instance to allocate</item>
/// </list>
///
/// <para><b>Action<c>&lt;T&gt;</c> / Action Cons</b></para>
/// <list type="bullet">
///   <item>Non-standard: .NET recommends EventHandler<c>&lt;T&gt;</c></item>
///   <item>CA1003 warning: requires suppression</item>
///   <item>No sender info (if needed in future, signature must change)</item>
/// </list>
///
/// <para><b>EventHandler<c>&lt;T&gt;</c> Pros</b></para>
/// <list type="bullet">
///   <item>.NET standard pattern</item>
///   <item>Provides sender (this) automatically</item>
///   <item>Extensible via EventArgs without breaking change</item>
///   <item>No suppression needed</item>
/// </list>
///
/// <para><b>EventHandler<c>&lt;T&gt;</c> Cons</b></para>
/// <list type="bullet">
///   <item>Extra 'this' argument even when unused</item>
///   <item>EventArgs allocation (performance cost in hot path)</item>
/// </list>
///
/// <para><b>Why Action Here?</b></para>
/// <list type="bullet">
///   <item>Internal callback only (not a public API)</item>
///   <item>Hot path: invoked frequently during indexing</item>
///   <item>Sender not needed (notification is the only concern)</item>
///   <item>Simplicity and zero-allocation preferred</item>
/// </list>
/// </remarks>
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

    /// <summary>Total file size in bytes. Set once before scanning begins.</summary>
    public long FileSize { get; private set; }

    /// <summary>
    /// Raised once when the first checkpoint (CheckPointInterval rows) has been
    /// indexed. Fired from the indexing thread; subscribers must not block.
    /// </summary>
#pragma warning disable CA1003 // See class <remarks> for rationale
    public event Action? FirstCheckpointReached;
#pragma warning restore CA1003

    /// <summary>
    /// Raised on every checkpoint boundary.
    /// Arguments: (bytesRead, fileSize).
    /// Fired from the indexing thread; subscribers must not block.
    /// </summary>
#pragma warning disable CA1003 // See class <remarks> for rationale
    public event Action<long, long>? ProgressChanged;
#pragma warning restore CA1003

    /// <summary>
    /// Raised once when BuildIndex returns — whether it completed normally,
    /// was cancelled, or threw an exception.
    /// Fired from the indexing thread (inside the finally block).
    /// </summary>
#pragma warning disable CA1003 // See class <remarks> for rationale
    public event Action? BuildIndexCompleted;
#pragma warning restore CA1003

    /// <summary>
    /// Bytes read so far. Updated atomically after each buffer read.
    /// Safe to read from any thread.
    /// </summary>
    public long BytesRead => Interlocked.Read(ref _bytesRead);

    private long _bytesRead;
    private bool _firstCheckpointReached;

    /// <summary>
    /// Builds the data row index by scanning the entire CSV file (header is skipped).
    /// Exits cooperatively when <paramref name="ct"/> is cancelled.
    /// NOT thread-safe — call once from a single background thread.
    /// <see cref="BuildIndexCompleted"/> fires unconditionally when this method
    /// returns, regardless of whether it completed, was cancelled, or threw.
    /// </summary>
    public void BuildIndex(CancellationToken ct = default)
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
            var fileOffset = 0L;
            var rowCount = 0L;
            var inQuotes = false;
            var lastByteRead = (byte)0;
            var headerSkipped = false;

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
                ProcessBuffer(span, ref fileOffset, ref rowCount, ref inQuotes, ref headerSkipped);

                fileOffset += bytesRead;
            }

            // If file doesn't end with newline, count the last line (skip header)
            if (Interlocked.Read(ref _bytesRead) > 0 && lastByteRead != (byte)'\n' && !inQuotes && headerSkipped)
            {
                rowCount++;
            }

            Interlocked.Exchange(ref _totalRows, rowCount);

            // Empty-file / sub-checkpoint guard: FirstCheckpointReached must
            // always fire so that the TaskCompletionSource in FileLoader does
            // not hang.
            if (!_firstCheckpointReached)
            {
                _firstCheckpointReached = true;
                FirstCheckpointReached?.Invoke();
            }
        }
        finally
        {
            // Guarantee FirstCheckpointReached fires even on cancellation or error,
            // so the TaskCompletionSource in FileLoader never hangs.
            if (!_firstCheckpointReached)
            {
                _firstCheckpointReached = true;
                FirstCheckpointReached?.Invoke();
            }

            ArrayPool<byte>.Shared.Return(buffer);
            BuildIndexCompleted?.Invoke();
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

            if (!_firstCheckpointReached)
            {
                _firstCheckpointReached = true;
                FirstCheckpointReached?.Invoke();
            }

            ProgressChanged?.Invoke(Interlocked.Read(ref _bytesRead), FileSize);
        }
    }
}
