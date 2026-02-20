using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Json;

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
    {
        if (lineBytes.IsEmpty)
        {
            return "<error>";
        }

        try
        {
            var reader = new Utf8JsonReader(lineBytes);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return "<error>";
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                if (!reader.ValueTextEquals(columnNameUtf8))
                {
                    reader.Skip();
                    continue;
                }

                if (!reader.Read())
                {
                    return "<error>";
                }

                return FormatValue(reader.TokenType, reader.ValueSpan);
            }

            return "<null>";
        }
        catch (JsonException)
        {
            return "<error>";
        }
    }

    private static string FormatValue(JsonTokenType tokenType, ReadOnlySpan<byte> valueSpan)
    {
        if (tokenType == JsonTokenType.Number)
        {
            return FormatNumber(valueSpan);
        }

        return tokenType switch
        {
            JsonTokenType.String => Encoding.UTF8.GetString(valueSpan),
            JsonTokenType.True => "True",
            JsonTokenType.False => "False",
            JsonTokenType.Null => "<null>",
            JsonTokenType.StartObject => "{...}",
            JsonTokenType.StartArray => "[...]",
            _ => "<null>",
        };
    }

    private static string FormatNumber(ReadOnlySpan<byte> valueSpan)
    {
        if (
            Utf8Parser.TryParse(valueSpan, out long intValue, out var bytesConsumed)
            && bytesConsumed == valueSpan.Length
        )
        {
            return intValue.ToString(CultureInfo.InvariantCulture);
        }

        if (
            Utf8Parser.TryParse(valueSpan, out double doubleValue, out var consumed)
            && consumed == valueSpan.Length
        )
        {
            return doubleValue.ToString(CultureInfo.InvariantCulture);
        }

        return "<error>";
    }
}
