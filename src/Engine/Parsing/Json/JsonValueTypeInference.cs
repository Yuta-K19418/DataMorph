using System.Buffers;
using System.Globalization;
using System.Text.Json;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.Parsing.Json;

/// <summary>
/// Provides methods for inferring column types from JSON token values.
/// Uses zero-allocation parsing techniques for high performance.
/// </summary>
internal static class JsonValueTypeInference
{
    /// <summary>
    /// Infers the column type from a JSON token type and value.
    /// </summary>
    /// <param name="tokenType">The JSON token type.</param>
    /// <param name="valueSpan">The raw UTF-8 bytes of the value (for String tokens).</param>
    /// <returns>The inferred column type.</returns>
    public static ColumnType InferType(JsonTokenType tokenType, ReadOnlySpan<byte> valueSpan = default)
    {
        return tokenType switch
        {
            JsonTokenType.Null => ColumnType.Null,
            JsonTokenType.True or JsonTokenType.False => ColumnType.Boolean,
            JsonTokenType.Number => InferNumericType(valueSpan),
            JsonTokenType.String => InferStringType(valueSpan),
            _ => ColumnType.Text // Default fallback for arrays, objects, etc.
        };
    }

    /// <summary>
    /// Infers whether a numeric value is a whole number or floating-point.
    /// </summary>
    private static ColumnType InferNumericType(ReadOnlySpan<byte> valueSpan)
    {
        // Check if the value contains a decimal point or exponent notation
        for (int i = 0; i < valueSpan.Length; i++)
        {
            byte b = valueSpan[i];
            if (b == '.' || b == 'e' || b == 'E')
            {
                return ColumnType.FloatingPoint;
            }
        }

        return ColumnType.WholeNumber;
    }

    /// <summary>
    /// Infers the type of a string value (timestamp vs text).
    /// Uses zero-allocation parsing to detect ISO 8601 timestamps.
    /// </summary>
    private static ColumnType InferStringType(ReadOnlySpan<byte> valueSpan)
    {
        // Try to parse as DateTimeOffset (ISO 8601 format)
        // Use stack allocation for small buffers
        // Note: Use GetMaxCharCount to ensure buffer is large enough for worst-case UTF-8 to UTF-16 conversion
        int maxCharCount = System.Text.Encoding.UTF8.GetMaxCharCount(valueSpan.Length);
        Span<char> charBuffer = maxCharCount <= 256
            ? stackalloc char[maxCharCount]
            : new char[maxCharCount];

        int charCount = System.Text.Encoding.UTF8.GetChars(valueSpan, charBuffer);
        ReadOnlySpan<char> stringValue = charBuffer[..charCount];

        // Check for ISO 8601 date patterns using exact formats
        // Supported formats:
        // - "yyyy-MM-dd" (date only)
        // - "yyyy-MM-ddTHH:mm:ss" (local time)
        // - "yyyy-MM-ddTHH:mm:ssZ" (UTC)
        // - "yyyy-MM-ddTHH:mm:ss.fff" (with milliseconds)
        // - "yyyy-MM-ddTHH:mm:ss.fffffffK" (with fractional seconds and timezone)
        if (DateTimeOffset.TryParseExact(
            stringValue,
            ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ",
             "yyyy-MM-ddTHH:mm:ss.fff", "yyyy-MM-ddTHH:mm:ss.ffffff",
             "yyyy-MM-ddTHH:mm:ss.fffffffK", "yyyy-MM-ddTHH:mm:sszzz"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _))
        {
            return ColumnType.Timestamp;
        }

        return ColumnType.Text;
    }

    /// <summary>
    /// Combines two column types to determine the most general type that can hold both.
    /// Used when multiple records have different types for the same column.
    /// </summary>
    /// <param name="type1">The first column type.</param>
    /// <param name="type2">The second column type.</param>
    /// <returns>The most general type that can represent both inputs.</returns>
    public static ColumnType CombineTypes(ColumnType type1, ColumnType type2)
    {
        // If types are identical, return that type
        if (type1 == type2)
        {
            return type1;
        }

        // Null can be combined with any type (makes it nullable)
        if (type1 == ColumnType.Null)
        {
            return type2;
        }
        if (type2 == ColumnType.Null)
        {
            return type1;
        }

        // WholeNumber + FloatingPoint = FloatingPoint
        if ((type1 == ColumnType.WholeNumber && type2 == ColumnType.FloatingPoint) ||
            (type1 == ColumnType.FloatingPoint && type2 == ColumnType.WholeNumber))
        {
            return ColumnType.FloatingPoint;
        }

        // Any other combination defaults to Text (most permissive)
        return ColumnType.Text;
    }
}
