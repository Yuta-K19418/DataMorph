namespace DataMorph.Engine.IO;

/// <summary>
/// Manages a sliding window cache of CSV data rows for efficient virtual scrolling.
/// Uses ReadOnlyMemory for memory-efficient column storage.
/// </summary>
public sealed class CsvDataRowCache
{
    private const int DefaultCacheSize = 200;
    private readonly CsvDataRowIndexer _indexer;
    private readonly CsvDataRowReader _reader;
    private readonly int _columnCount;
    private readonly int _cacheSize;
    private readonly Dictionary<int, CsvDataRow> _cache = [];
    private int _cacheStartRow = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvDataRowCache"/> class.
    /// </summary>
    /// <param name="indexer">The CSV row indexer for obtaining byte offsets.</param>
    /// <param name="columnCount">The number of columns in the CSV.</param>
    /// <param name="cacheSize">The size of the sliding window cache (default: 200).</param>
    public CsvDataRowCache(
        CsvDataRowIndexer indexer,
        int columnCount,
        int cacheSize = DefaultCacheSize
    )
    {
        _indexer = indexer;
        _columnCount = columnCount;
        _cacheSize = cacheSize;
        ArgumentNullException.ThrowIfNull(indexer);
        _reader = new CsvDataRowReader(indexer.FilePath, columnCount);
    }

    /// <summary>
    /// Gets the total number of rows in the CSV.
    /// </summary>
    public int TotalRows => (int)_indexer.TotalRows;

    /// <summary>
    /// Retrieves a row by its index, using the cache when possible.
    /// </summary>
    /// <param name="rowIndex">The zero-based row index.</param>
    /// <returns>The CSV row, or an empty row if not available.</returns>
    public CsvDataRow GetRow(int rowIndex)
    {
        // No data, nothing to do
        if (TotalRows == 0)
        {
            return [];
        }

        if (rowIndex < 0 || rowIndex >= TotalRows)
        {
            return [];
        }

        if (!_cache.ContainsKey(rowIndex))
        {
            UpdateCache(rowIndex);
        }

        return _cache.TryGetValue(rowIndex, out var cachedRow) ? cachedRow : [];
    }

    private void UpdateCache(int requestedRow)
    {
        // Calculate cache window start row
        var cacheStartRow = CalculateCacheStartRow(requestedRow);

        // Calculate number of rows to fetch
        var rowsToFetch = CalculateRowsToFetch(cacheStartRow);
        if (rowsToFetch <= 0)
        {
            _cache.Clear();
            _cacheStartRow = -1;
            return;
        }

        // Get byte offset in file
        var (byteOffset, rowOffsetToSkip) = _indexer.GetCheckPoint(cacheStartRow);

        // Abort if offset is invalid
        if (byteOffset < 0)
        {
            return;
        }

        // Clear current cache and load new data
        _cache.Clear();
        _cacheStartRow = cacheStartRow;

        // Load rows from CSV file into cache
        LoadRowsIntoCache(cacheStartRow, byteOffset, rowOffsetToSkip, rowsToFetch);
    }

    /// <summary>
    /// Calculates the start row of the cache window centered around the requested row.
    /// </summary>
    /// <param name="requestedRow">The row index requested by the user.</param>
    /// <returns>The start row of the cache window.</returns>
    private int CalculateCacheStartRow(int requestedRow)
    {
        // If cache size is larger than total rows, start at row 0
        if (_cacheSize >= TotalRows)
        {
            return 0;
        }

        var halfCacheSize = _cacheSize / 2;
        var cacheStartRow = Math.Max(0, requestedRow - halfCacheSize);

        var maxValidStartRow = TotalRows - _cacheSize;
        return Math.Min(cacheStartRow, maxValidStartRow);
    }

    /// <summary>
    /// Calculates the number of rows to fetch from the cache window.
    /// </summary>
    /// <param name="cacheStartRow">The start row of the cache window.</param>
    /// <returns>The number of rows to fetch.</returns>
    private int CalculateRowsToFetch(int cacheStartRow)
    {
        // Return the smaller of remaining rows and cache size
        var remainingRows = TotalRows - cacheStartRow;
        return Math.Min(_cacheSize, remainingRows);
    }

    /// <summary>
    /// Loads rows from the CSV file and stores them in the cache.
    /// </summary>
    /// <param name="cacheStartRow">The start row of the cache window.</param>
    /// <param name="byteOffset">The byte offset within the file.</param>
    /// <param name="rowOffsetToSkip">The row offset to skip.</param>
    /// <param name="rowsToFetch">The number of rows to fetch.</param>
    private void LoadRowsIntoCache(
        int cacheStartRow,
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch
    )
    {
        // Read rows from CSV file
        var rows = _reader.ReadRows(byteOffset, rowOffsetToSkip, rowsToFetch);

        // Store read rows in cache
        var currentRowIndex = cacheStartRow;
        foreach (var row in rows)
        {
            _cache[currentRowIndex] = row;
            currentRowIndex++;
        }
    }
}
