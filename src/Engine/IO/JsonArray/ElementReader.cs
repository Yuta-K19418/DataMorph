namespace DataMorph.Engine.IO.JsonArray;

/// <summary>
/// Reads raw JSON element bytes from a JSON Array file given a checkpoint byte offset.
/// Uses <see cref="MmapService"/> for consistent memory-mapped random access and
/// <see cref="System.Text.Json.Utf8JsonReader"/> to locate element boundaries.
/// </summary>
public sealed class ElementReader : IDisposable
{
    private readonly MmapService _mmap;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementReader"/> class.
    /// </summary>
    /// <param name="filePath">Path to the JSON Array file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the file cannot be memory-mapped.</exception>
    public ElementReader(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var mmapResult = MmapService.Open(filePath);
        if (mmapResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to open memory-mapped file: {mmapResult.Error.ToString()}"
            );
        }

        _mmap = mmapResult.Value;
    }

    /// <summary>
    /// Reads raw JSON bytes for elements starting at <paramref name="byteOffset"/>.
    /// </summary>
    /// <param name="byteOffset">A checkpoint byte offset from <see cref="RowIndexer.GetCheckPoint"/>.</param>
    /// <param name="elementsToSkip">Number of elements to skip before collecting (non-negative).</param>
    /// <param name="elementsToFetch">Maximum elements to collect after skipping (non-negative).</param>
    /// <returns>Raw JSON bytes per element (NOT TreeNode — no App layer dependency).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for negative <paramref name="elementsToSkip"/> or <paramref name="elementsToFetch"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose"/> has been called.</exception>
    public IReadOnlyList<ReadOnlyMemory<byte>> ReadElementBytes(
        long byteOffset,
        int elementsToSkip,
        int elementsToFetch
    )
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mmap.Dispose();
        _disposed = true;
    }
}
