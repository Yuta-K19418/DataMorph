namespace DataMorph.Engine.Types;

/// <summary>
/// Supported column data types for schema inference.
/// </summary>
public enum ColumnType
{
    /// <summary>
    /// Text data (string).
    /// </summary>
    Text,

    /// <summary>
    /// Whole number (int64).
    /// </summary>
    WholeNumber,

    /// <summary>
    /// Floating-point number (double).
    /// </summary>
    FloatingPoint,

    /// <summary>
    /// Boolean true/false value.
    /// </summary>
    Boolean,

    /// <summary>
    /// Date and time value.
    /// </summary>
    Timestamp,

    /// <summary>
    /// Null value (used when all values are null).
    /// </summary>
    Null
}
