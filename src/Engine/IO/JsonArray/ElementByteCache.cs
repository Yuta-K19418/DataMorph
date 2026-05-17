namespace DataMorph.Engine.IO.JsonArray;

/// <summary>
/// Sliding window LRU cache for JSON Array element bytes.
/// Delegates byte loading to <see cref="ElementReader"/>.
/// </summary>
public sealed class ElementByteCache(
    IRowIndexer indexer,
    int capacity = 200,
    int prefetchWindow = 20)
    : SlidingWindowLruCache<ReadOnlyMemory<byte>>(indexer, capacity, prefetchWindow), IDisposable
{
    private readonly ElementReader _reader = new(indexer.FilePath);
    private bool _disposed;

    /// <inheritdoc/>
    public override ReadOnlyMemory<byte> GetRow(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return base.GetRow(index);
    }

    /// <inheritdoc/>
    protected override ReadOnlyMemory<byte> EmptyValue => ReadOnlyMemory<byte>.Empty;

    /// <inheritdoc/>
    protected override IEnumerable<ReadOnlyMemory<byte>> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch) =>
        _reader.ReadElementBytes(byteOffset, rowOffsetToSkip, rowsToFetch);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _reader.Dispose();
        _disposed = true;
    }
}
