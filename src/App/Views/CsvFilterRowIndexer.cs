using DataMorph.Engine.IO.Csv;

namespace DataMorph.App.Views;

/// <summary>
/// CSV-specific implementation of <see cref="IFilterRowIndexer"/>.
/// Uses <see cref="DataRowIndexer"/> for row checkpoints and a dedicated
/// <see cref="DataRowReader"/> (separate from the display cache) for sequential
/// row scanning, limiting total file I/O to approximately one full pass.
/// </summary>
internal sealed class CsvFilterRowIndexer : IFilterRowIndexer
{
    private readonly DataRowIndexer _indexer;
    private readonly int _sourceColumnCount;
    private readonly IReadOnlyList<FilterSpec> _filterSpecs;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvFilterRowIndexer"/>.
    /// </summary>
    /// <param name="indexer">Completed row indexer for the CSV source file.</param>
    /// <param name="sourceColumnCount">Number of columns in the source schema.</param>
    /// <param name="filterSpecs">Resolved filter specifications to apply.</param>
    internal CsvFilterRowIndexer(
        DataRowIndexer indexer,
        int sourceColumnCount,
        IReadOnlyList<FilterSpec> filterSpecs
    )
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(filterSpecs);
        _indexer = indexer;
        _sourceColumnCount = sourceColumnCount;
        _filterSpecs = filterSpecs;
    }

    /// <inheritdoc/>
    public int TotalMatchedRows => throw new NotImplementedException();

    /// <inheritdoc/>
    public int GetSourceRow(int filteredRow) => throw new NotImplementedException();

    /// <inheritdoc/>
    public Task BuildIndexAsync(CancellationToken ct) => throw new NotImplementedException();
}
