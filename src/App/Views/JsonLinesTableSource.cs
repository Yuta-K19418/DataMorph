using System.Text;
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
    private volatile TableSchema _schema;
    private volatile string[] _columnNames;
    private volatile byte[][] _columnNameUtf8;

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
        _columnNames = BuildColumnNames(schema);
        _columnNameUtf8 = BuildColumnNamesUtf8(schema);
    }

    /// <inheritdoc/>
    public int Rows => _cache.TotalLines;

    /// <inheritdoc/>
    public int Columns => _schema.ColumnCount;

    /// <inheritdoc/>
    public string[] ColumnNames => _columnNames;

    /// <inheritdoc/>
    public object this[int row, int col]
    {
        get
        {
            if (row < 0 || row >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (col < 0 || col >= Columns)
            {
                throw new ArgumentOutOfRangeException(nameof(col));
            }

            var lineBytes = _cache.GetLineBytes(row);
            if (lineBytes.IsEmpty)
            {
                return "<null>";
            }

            return CellExtractor.ExtractCell(lineBytes.Span, _columnNameUtf8[col]);
        }
    }

    /// <summary>
    /// Atomically replaces the current schema with a refined version.
    /// Called from the background schema scan when new columns are discovered.
    /// Rebuilds pre-encoded column name arrays before updating the schema reference
    /// so the UI thread always sees a consistent snapshot.
    /// </summary>
    /// <param name="schema">The refined schema to apply.</param>
    public void UpdateSchema(TableSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var newColumnNames = BuildColumnNames(schema);
        var newColumnNameUtf8 = BuildColumnNamesUtf8(schema);
        _columnNames = newColumnNames;
        _columnNameUtf8 = newColumnNameUtf8;
        _schema = schema;
    }

    private static string[] BuildColumnNames(TableSchema schema) =>
        [.. schema.Columns.Select(c => c.Name)];

    private static byte[][] BuildColumnNamesUtf8(TableSchema schema) =>
        [.. schema.Columns.Select(c => Encoding.UTF8.GetBytes(c.Name))];
}
