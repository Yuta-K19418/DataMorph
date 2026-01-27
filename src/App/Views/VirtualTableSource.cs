using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Provides virtual table data source for Terminal.Gui's TableView.
/// Delegates to CsvDataRowCache for efficient row retrieval.
/// </summary>
internal sealed class VirtualTableSource : ITableSource
{
    private readonly CsvDataRowCache _cache;
    private readonly TableSchema _schema;
    private readonly string[] _columnNames;

    public VirtualTableSource(CsvDataRowIndexer indexer, TableSchema schema)
    {
        _schema = schema;
        _columnNames = _schema.Columns.Select(c => c.Name).ToArray();
        _cache = new CsvDataRowCache(indexer, _schema.ColumnCount);
    }

    public int Rows => _cache.TotalRows;
    public int Columns => _schema.ColumnCount;
    public string[] ColumnNames => _columnNames;

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

            var rowData = _cache.GetRow(row);

            if (col < rowData.Count)
            {
                var memory = rowData[col];
                return memory.IsEmpty ? string.Empty : new string(memory.Span);
            }

            // Return empty string for columns that might not exist in a ragged CSV row
            return string.Empty;
        }
    }
}
