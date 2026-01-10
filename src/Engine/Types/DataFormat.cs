namespace DataMorph.Engine.Types;

/// <summary>
/// Supported data file formats for DataMorph.
/// </summary>
public enum DataFormat
{
    /// <summary>
    /// Comma-Separated Values (CSV) format.
    /// Delimited by comma (,).
    /// </summary>
    Csv,

    /// <summary>
    /// JSON Lines format (one JSON object per line).
    /// Each line contains a complete JSON object: {"key": "value"}\n
    /// </summary>
    JsonLines,

    /// <summary>
    /// JSON Array format.
    /// Root element is an array: [...]
    /// </summary>
    JsonArray,

    /// <summary>
    /// JSON Object format.
    /// Root element is an object: {...}
    /// </summary>
    JsonObject,
}
