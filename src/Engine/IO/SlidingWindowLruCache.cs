namespace DataMorph.Engine.IO;

/// <summary>
/// Abstract base class implementing a sliding window LRU cache for row-based data.
/// Combines prefetch-based loading (centered on requested row) with LRU eviction
/// to optimize sequential and bidirectional scrolling scenarios.
/// </summary>
/// <typeparam name="TRow">The type of row data.</typeparam>
/// <remarks>
/// Thread Safety: This implementation is not thread-safe by design.
/// All access is assumed to occur on the TUI rendering thread (single-threaded context).
/// Future parallel access scenarios would require synchronization.
/// </remarks>
public abstract class SlidingWindowLruCache<TRow>
{
    private const int DefaultCapacity = 200;
    private const int DefaultPrefetchWindow = 20;

    /// <summary>
    /// Initializes the LRU cache with the given indexer, capacity, and prefetch window size.
    /// </summary>
    protected SlidingWindowLruCache(
        IRowIndexer indexer,
        int capacity = DefaultCapacity,
        int prefetchWindow = DefaultPrefetchWindow) => throw new NotImplementedException();

    /// <inheritdoc/>
#pragma warning disable CA1065
    public int TotalRows => throw new NotImplementedException();
#pragma warning restore CA1065

    /// <inheritdoc/>
    public TRow GetRow(int index) => throw new NotImplementedException();

    /// <summary>
    /// Gets the empty/default value for the row type, used to reset cache entries.
    /// </summary>
    protected abstract TRow EmptyValue { get; }

    /// <summary>
    /// Loads a range of rows from the underlying data source.
    /// </summary>
    /// <param name="byteOffset">The byte offset within the file to start reading from.</param>
    /// <param name="rowOffsetToSkip">The number of rows to skip from the byte offset.</param>
    /// <param name="rowsToFetch">The number of rows to fetch.</param>
    /// <returns>An enumerable of row data.</returns>
    protected abstract IEnumerable<TRow> LoadRows(
        long byteOffset,
        int rowOffsetToSkip,
        int rowsToFetch);
}
