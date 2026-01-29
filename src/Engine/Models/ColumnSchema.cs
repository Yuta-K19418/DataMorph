using DataMorph.Engine.Types;

namespace DataMorph.Engine.Models;

/// <summary>
/// Represents the schema information for a single column in the dataset.
/// Used for schema discovery and type inference during JSON/CSV parsing.
/// </summary>
public sealed record ColumnSchema
{
    /// <summary>
    /// The name of the column (e.g., "id", "user.name", "order.details.price" for flattened JSON).
    /// </summary>
    public required string Name
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    }

    /// <summary>
    /// The inferred or explicit data type of the column.
    /// </summary>
    public required ColumnType Type { get; set; }

    /// <summary>
    /// Indicates whether this column can contain null values.
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// The zero-based index position of this column in the table.
    /// </summary>
    public int ColumnIndex
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    }

    /// <summary>
    /// Optional: Format string for display (e.g., date format, number precision).
    /// </summary>
    public string? DisplayFormat { get; init; }
}
