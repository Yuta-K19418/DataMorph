using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// ITableSource backed by pre-materialized child object bytes.
/// Renders the # column using format-specific formatting.
/// </summary>
internal sealed class FocusedTableSource : ITableSource
{
    internal FocusedTableSource(DrillDownState drillDown) =>
        throw new NotImplementedException();

    /// <inheritdoc/>
    public int Rows => throw new NotImplementedException();

    /// <inheritdoc/>
    public int Columns => throw new NotImplementedException();

    /// <inheritdoc/>
    public string[] ColumnNames => throw new NotImplementedException();

    /// <inheritdoc/>
    public object this[int row, int col] => throw new NotImplementedException();

    /// <summary>
    /// Formats the # column value for the given row index using format-specific rules.
    /// JSON Lines / JSON Array: {recordPosition}:{row}; JSON Object: [{row}].
    /// </summary>
    private string FormatHashColumn(int row) => throw new NotImplementedException();
}
