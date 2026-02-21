using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.App.Views;

/// <summary>
/// JSON Lines-specific implementation of <see cref="IFilterRowIndexer"/>.
/// Uses <see cref="RowIndexer"/> for row checkpoints and a dedicated
/// <see cref="RowReader"/> (separate from the display cache) for sequential
/// row scanning, limiting total file I/O to approximately one full pass.
/// </summary>
internal sealed class JsonLinesFilterRowIndexer : IFilterRowIndexer
{
    private readonly RowIndexer _indexer;
    private readonly string _filePath;
    private readonly IReadOnlyList<byte[]> _columnNamesUtf8;
    private readonly IReadOnlyList<FilterSpec> _filterSpecs;

    /// <summary>
    /// Initializes a new instance of <see cref="JsonLinesFilterRowIndexer"/>.
    /// </summary>
    /// <param name="indexer">Completed row indexer for the JSON Lines source file.</param>
    /// <param name="filePath">Path to the JSON Lines source file.</param>
    /// <param name="columnNamesUtf8">UTF-8 encoded column names for <see cref="CellExtractor"/> lookups.</param>
    /// <param name="filterSpecs">Resolved filter specifications to apply.</param>
    internal JsonLinesFilterRowIndexer(
        RowIndexer indexer,
        string filePath,
        IReadOnlyList<byte[]> columnNamesUtf8,
        IReadOnlyList<FilterSpec> filterSpecs
    )
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(columnNamesUtf8);
        ArgumentNullException.ThrowIfNull(filterSpecs);
        _indexer = indexer;
        _filePath = filePath;
        _columnNamesUtf8 = columnNamesUtf8;
        _filterSpecs = filterSpecs;
    }

    /// <inheritdoc/>
    public int TotalMatchedRows => throw new NotImplementedException();

    /// <inheritdoc/>
    public int GetSourceRow(int filteredRow) => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task BuildIndexAsync(CancellationToken ct) => throw new NotImplementedException();
}
