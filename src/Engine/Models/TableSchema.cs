using DataMorph.Engine.Types;

namespace DataMorph.Engine.Models;

/// <summary>
/// Represents the complete schema of a data table, including all columns.
/// Built through schema discovery when loading JSON/CSV files.
/// </summary>
public sealed record TableSchema
{
    /// <summary>
    /// The ordered list of columns in the table.
    /// </summary>
    public required IReadOnlyList<ColumnSchema> Columns { get; init; }

    /// <summary>
    /// The total number of rows in the dataset (if known).
    /// May be null for streaming data where row count is unknown.
    /// </summary>
    public long? RowCount { get; init; }

    /// <summary>
    /// The source file format (Json or Csv).
    /// </summary>
    public required DataFormat SourceFormat { get; init; }

    /// <summary>
    /// Returns the number of columns in the schema.
    /// </summary>
    public int ColumnCount => Columns.Count;

    /// <summary>
    /// Gets a column by name, or null if not found.
    /// </summary>
    public ColumnSchema? GetColumn(string name) =>
        Columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.Ordinal));

    /// <summary>
    /// Checks if a column with the specified name exists in the schema.
    /// </summary>
    public bool ContainsColumn(string name) =>
        Columns.Any(c => c.Name.Equals(name, StringComparison.Ordinal));
}
