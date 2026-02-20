using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Provides virtual table data source for Terminal.Gui's TableView for JSON Lines files.
/// Delegates to RowByteCache for line retrieval and CellExtractor for cell value parsing.
/// </summary>
internal sealed class JsonLinesTableSource : ITableSource
{
    private readonly RowByteCache _cache;
    private readonly TableSchema _schema;

    /// <summary>
    /// Initializes a new instance of <see cref="JsonLinesTableSource"/>.
    /// </summary>
    /// <param name="cache">Row byte cache for line data retrieval.</param>
    /// <param name="schema">Initial schema from first N lines.</param>
    public JsonLinesTableSource(RowByteCache cache, TableSchema schema)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(schema);
        _cache = cache;
        _schema = schema;
    }

    /// <inheritdoc/>
    public int Rows => throw new NotImplementedException();

    /// <inheritdoc/>
    public int Columns => throw new NotImplementedException();

    /// <inheritdoc/>
    public string[] ColumnNames => throw new NotImplementedException();

    /// <inheritdoc/>
    public object this[int row, int col] => throw new NotImplementedException();

    /// <summary>
    /// Atomically replaces the current schema with a refined version.
    /// Called from the background schema scan when new columns are discovered.
    /// Rebuilds pre-encoded column name arrays.
    /// </summary>
    /// <param name="schema">The refined schema to apply.</param>
    public void UpdateSchema(TableSchema schema) => throw new NotImplementedException();
}
