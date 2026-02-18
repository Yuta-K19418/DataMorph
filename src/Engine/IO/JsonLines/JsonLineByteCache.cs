namespace DataMorph.Engine.IO.JsonLines;

/// <summary>
/// Manages a sliding window cache of JSON line bytes for efficient virtual scrolling.
/// Uses ReadOnlyMemory&lt;byte&gt; for memory-efficient line storage.
/// </summary>
public sealed class JsonLineByteCache : IDisposable
{
    private const int DefaultCacheSize = 200;
    private readonly RowIndexer _indexer;
    private readonly JsonLineReader _reader;
    private readonly int _cacheSize;
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _cache = [];
    private int _cacheStartRow = -1;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLineByteCache"/> class.
    /// </summary>
    /// <param name="indexer">The row indexer for obtaining byte offsets.</param>
    /// <param name="cacheSize">The size of the sliding window cache (default: 200).</param>
    public JsonLineByteCache(RowIndexer indexer, int cacheSize = DefaultCacheSize)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        _indexer = indexer;
        _cacheSize = cacheSize;
        _reader = new JsonLineReader(indexer.FilePath);
    }

    /// <summary>
    /// Gets the total number of lines in the JSON Lines file.
    /// </summary>
    public int TotalLines => (int)_indexer.TotalRows;

    /// <summary>
    /// Retrieves raw JSON bytes for a line by its index, using the cache when possible.
    /// </summary>
    /// <param name="lineIndex">The zero-based line index.</param>
    /// <returns>The raw JSON bytes, or empty if not available.</returns>
    public ReadOnlyMemory<byte> GetLineBytes(int lineIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (TotalLines == 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        if (lineIndex < 0 || lineIndex >= TotalLines)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        if (!_cache.ContainsKey(lineIndex))
        {
            UpdateCache(lineIndex);
        }

        return _cache.TryGetValue(lineIndex, out var cachedBytes) ? cachedBytes : ReadOnlyMemory<byte>.Empty;
    }

    private void UpdateCache(int requestedRow)
    {
        var cacheStartRow = CalculateCacheStartRow(requestedRow);
        var rowsToFetch = CalculateRowsToFetch(cacheStartRow);

        if (rowsToFetch <= 0)
        {
            _cache.Clear();
            _cacheStartRow = -1;
            return;
        }

        var (byteOffset, rowOffsetToSkip) = _indexer.GetCheckPoint(cacheStartRow);

        if (byteOffset < 0)
        {
            return;
        }

        _cache.Clear();
        _cacheStartRow = cacheStartRow;

        LoadRowsIntoCache(cacheStartRow, byteOffset, rowOffsetToSkip, rowsToFetch);
    }

    /// <summary>
    /// Calculates the start row of the cache window centered around the requested row.
    /// </summary>
    private int CalculateCacheStartRow(int requestedRow)
    {
        if (_cacheSize >= TotalLines)
        {
            return 0;
        }

        var halfCacheSize = _cacheSize / 2;
        var cacheStartRow = Math.Max(0, requestedRow - halfCacheSize);
        var maxValidStartRow = TotalLines - _cacheSize;

        return Math.Min(cacheStartRow, maxValidStartRow);
    }

    /// <summary>
    /// Calculates the number of rows to fetch from the cache window.
    /// </summary>
    private int CalculateRowsToFetch(int cacheStartRow)
    {
        var remainingRows = TotalLines - cacheStartRow;
        return Math.Min(_cacheSize, remainingRows);
    }

    /// <summary>
    /// Loads lines from the JSON Lines file and stores them in the cache.
    /// </summary>
    private void LoadRowsIntoCache(
        int cacheStartRow,
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch
    )
    {
        var lines = _reader.ReadLineBytes(byteOffset, rowOffsetToSkip, rowsToFetch);

        var currentRowIndex = cacheStartRow;
        foreach (var line in lines)
        {
            _cache[currentRowIndex] = line;
            currentRowIndex++;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _reader.Dispose();
            _disposed = true;
        }
    }
}
