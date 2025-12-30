using System.Diagnostics;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.Models;

/// <summary>
/// Represents the complete schema of a data table, including all columns.
/// Built through schema discovery when loading JSON/CSV files.
/// </summary>
public sealed record TableSchema
{
    private readonly Dictionary<string, ColumnSchema>? _columnCache;

    /// <summary>
    /// The ordered list of columns in the table.
    /// </summary>
    public required IReadOnlyList<ColumnSchema> Columns
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            // Validate for duplicate column names using HashSet for O(1) lookup
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var column in value)
            {
                if (!seen.Add(column.Name))
                {
                    throw new ArgumentException(
                        $"Duplicate column name found: {column.Name}",
                        nameof(Columns));
                }
            }

            field = value;

            // Initialize cache in init block for thread-safety and performance
            _columnCache = value.ToDictionary(c => c.Name, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The total number of rows in the dataset.
    /// Use 0 if the row count is unknown (e.g., for streaming data).
    /// </summary>
    public long RowCount
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

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
        (_columnCache ?? throw new UnreachableException()).GetValueOrDefault(name);

    /// <summary>
    /// Checks if a column with the specified name exists in the schema.
    /// Uses O(1) dictionary lookup for improved performance.
    /// </summary>
    public bool ContainsColumn(string name) =>
        (_columnCache ?? throw new UnreachableException()).ContainsKey(name);
}
