using System.Text;
using DataMorph.Engine.IO.Json;
using DataMorph.Engine.Models;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// ITableSource backed by pre-materialized child object bytes.
/// Renders the # column using format-specific formatting.
/// </summary>
internal sealed class FocusedTableSource : ITableSource
{
    private readonly IReadOnlyList<JsonRawBytes> _childValueBytes;
    private readonly TableSchema _schema;
    private readonly long? _recordPosition;
    private readonly string[] _columnNames;
    private readonly byte[][] _columnNamesUtf8;

    internal FocusedTableSource(DrillDownState drillDown)
    {
        ArgumentNullException.ThrowIfNull(drillDown);
        _childValueBytes = drillDown.ChildValueBytes;
        _schema = drillDown.Schema;
        _recordPosition = drillDown.RecordPosition;
        _columnNames = ["#", .. drillDown.Schema.Columns.Select(c => c.Name)];
        _columnNamesUtf8 = [.. drillDown.Schema.Columns.Select(c => Encoding.UTF8.GetBytes(c.Name))];
    }

    /// <inheritdoc/>
    public int Rows => _childValueBytes.Count;

    /// <inheritdoc/>
    public int Columns => _schema.ColumnCount + 1;

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

            if (col == 0)
            {
                return FormatHashColumn(row);
            }

            return JsonObjectCellExtractor.ExtractCell(_childValueBytes[row].Span, _columnNamesUtf8[col - 1]);
        }
    }

    /// <summary>
    /// Formats the # column value for the given row index using format-specific rules.
    /// JSON Lines / JSON Array: {recordPosition}:{row}; JSON Object: [{row}].
    /// </summary>
    private string FormatHashColumn(int row) =>
        _recordPosition is { } pos ? $"{pos}:{row}" : $"[{row}]";
}
