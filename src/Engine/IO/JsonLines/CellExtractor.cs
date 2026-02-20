namespace DataMorph.Engine.IO.JsonLines;

/// <summary>
/// Extracts cell values from raw JSON Lines bytes by column name.
/// Uses Utf8JsonReader for zero-allocation scanning of top-level properties.
/// </summary>
public static class CellExtractor
{
    /// <summary>
    /// Extracts a cell value from a raw JSON line by column name.
    /// </summary>
    /// <param name="lineBytes">Raw bytes of a single JSON Lines row.</param>
    /// <param name="columnNameUtf8">Pre-encoded UTF-8 bytes of the target column name.</param>
    /// <returns>
    /// String representation of the cell value.
    /// Returns "&lt;null&gt;" for missing keys or JSON null.
    /// Returns "&lt;error&gt;" for malformed JSON.
    /// </returns>
    public static string ExtractCell(ReadOnlySpan<byte> lineBytes, ReadOnlySpan<byte> columnNameUtf8)
        => throw new NotImplementedException();
}
