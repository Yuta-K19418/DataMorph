namespace DataMorph.Engine.IO.Csv;

/// <summary>
/// Manages a sliding window LRU cache of CSV data rows for efficient virtual scrolling.
/// Uses ReadOnlyMemory for memory-efficient column storage.
/// </summary>
public sealed class DataRowCache(
    IRowIndexer indexer,
    int columnCount,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<CsvDataRow>(indexer, capacity, prefetchWindow)
{
    private readonly DataRowReader _reader = new(indexer.FilePath, columnCount);

    /// <inheritdoc/>
    protected override CsvDataRow EmptyValue => [];

    /// <inheritdoc/>
    protected override IEnumerable<CsvDataRow> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch) =>
        _reader.ReadRows(byteOffset, rowOffsetToSkip, rowsToFetch);
}
