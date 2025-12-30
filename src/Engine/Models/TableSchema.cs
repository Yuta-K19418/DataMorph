using DataMorph.Engine.Types;

namespace DataMorph.Engine.Models;

/// <summary>
/// Represents the complete schema of a data table, including all columns.
/// Built through schema discovery when loading JSON/CSV files.
/// </summary>
public sealed record TableSchema
{
    private Dictionary<string, ColumnSchema>? _columnCache;

    private Dictionary<string, ColumnSchema> ColumnCache =>
        _columnCache ??= Columns.ToDictionary(c => c.Name, StringComparer.Ordinal);

    /// <summary>
    /// The ordered list of columns in the table.
    /// </summary>
    public required IReadOnlyList<ColumnSchema> Columns
    {
        get;
        init
        {
            // Validate for duplicate column names
            var firstDuplicate = value
                .GroupBy(c => c.Name, StringComparer.Ordinal)
                .FirstOrDefault(g => g.Count() > 1);

            if (firstDuplicate is not null)
            {
                throw new ArgumentException(
                    $"Duplicate column name found: {firstDuplicate.Key}",
                    nameof(Columns));
            }

            field = value;
        }
    }

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
    /// Uses O(1) dictionary lookup for improved performance.
    /// </summary>
    public ColumnSchema? GetColumn(string name) =>
        ColumnCache.GetValueOrDefault(name);

    /// <summary>
    /// Checks if a column with the specified name exists in the schema.
    /// Uses O(1) dictionary lookup for improved performance.
    /// </summary>
    public bool ContainsColumn(string name) =>
        ColumnCache.ContainsKey(name);
}
