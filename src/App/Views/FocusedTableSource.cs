using System.Text;
using DataMorph.Engine.IO.DrillDown;
using DataMorph.Engine.IO.Json;
using DataMorph.Engine.Models;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// ITableSource backed by pre-materialized <see cref="FocusedTableRow"/> rows.
/// </summary>
internal sealed class FocusedTableSource : ITableSource
{
    private readonly IReadOnlyList<FocusedTableRow> _rows;
    private readonly TableSchema _schema;
    private readonly string[] _columnNames;
    private readonly byte[][] _columnNamesUtf8;

    internal FocusedTableSource(DrillDownState drillDown)
    {
        ArgumentNullException.ThrowIfNull(drillDown);
        _rows = drillDown.Rows;
        _schema = drillDown.Schema;
        _columnNames = ["#", .. drillDown.Schema.Columns.Select(c => c.Name)];
        _columnNamesUtf8 = [.. drillDown.Schema.Columns.Select(c => Encoding.UTF8.GetBytes(c.Name))];
    }

    /// <inheritdoc/>
    public int Rows => _rows.Count;

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
                return _rows[row].HashValue;
            }

            return JsonObjectCellExtractor.ExtractCell(_rows[row].Bytes.Span, _columnNamesUtf8[col - 1]);
        }
    }
}
