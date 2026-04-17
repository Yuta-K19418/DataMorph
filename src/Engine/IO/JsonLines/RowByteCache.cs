namespace DataMorph.Engine.IO.JsonLines;

/// <summary>
/// Manages a sliding window LRU cache of JSON line bytes for efficient virtual scrolling.
/// Uses ReadOnlyMemory&lt;byte&gt; for memory-efficient line storage.
/// </summary>
public sealed class RowByteCache(
    IRowIndexer indexer,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<ReadOnlyMemory<byte>>(indexer, capacity, prefetchWindow), IDisposable
{

    /// <inheritdoc/>
    protected override ReadOnlyMemory<byte> EmptyValue => ReadOnlyMemory<byte>.Empty;

    /// <inheritdoc/>
    protected override IEnumerable<ReadOnlyMemory<byte>> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch) => throw new NotImplementedException();

    /// <inheritdoc/>
#pragma warning disable CA1065
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1065
}
