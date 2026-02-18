using System.Buffers.Text;
using System.Text.Json;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.IO.JsonLines;

/// <summary>
/// Infers column type from JSON token data.
/// Designed to work with Utf8JsonReader in the JSON Lines pipeline.
/// </summary>
/// <remarks>
/// Methods accept <see cref="JsonTokenType"/> and <see cref="ReadOnlySpan{T}"/> directly
/// rather than <c>ref Utf8JsonReader</c>, making it explicit that no reader state is mutated.
/// </remarks>
public static class TypeInferrer
{
    /// <summary>
    /// Reads the token type and raw value span and returns the inferred ColumnType.
    /// Caller must ensure the token is NOT <see cref="JsonTokenType.Null"/> before calling
    /// (use <see cref="IsNullToken"/> first).
    /// Returns <see cref="ColumnType.Text"/> as fallback for any unexpected tokens.
    /// </summary>
    /// <param name="tokenType">The current JSON token type.</param>
    /// <param name="valueSpan">
    /// The raw UTF-8 bytes of the current token value (i.e. <c>reader.ValueSpan</c>).
    /// Only used when <paramref name="tokenType"/> is <see cref="JsonTokenType.Number"/>.
    /// </param>
    public static ColumnType InferType(JsonTokenType tokenType, ReadOnlySpan<byte> valueSpan)
    {
        if (tokenType == JsonTokenType.Number)
        {
            // Utf8Parser.TryParse operates directly on the UTF-8 byte span,
            // avoiding string allocation. It is equivalent to what
            // Utf8JsonReader.TryGetInt64 does internally, but does not require
            // passing ref Utf8JsonReader and makes the absence of side effects explicit.
            // bytesConsumed == valueSpan.Length guards against partial matches
            // (e.g. "123abc"), which cannot occur for valid JSON tokens but is
            // included for correctness.
            if (
                Utf8Parser.TryParse(valueSpan, out long _, out var bytesConsumed)
                && bytesConsumed == valueSpan.Length
            )
            {
                return ColumnType.WholeNumber;
            }

            // Decimal point present (invariant: '.' only) → FloatingPoint.
            // Numbers without '.' that overflow int64 (e.g. big integers) → Text.
            if (valueSpan.Contains((byte)'.'))
            {
                return ColumnType.FloatingPoint;
            }

            return ColumnType.Text;
        }

        if (tokenType == JsonTokenType.String)
        {
            return ColumnType.Text;
        }

        if (tokenType == JsonTokenType.True || tokenType == JsonTokenType.False)
        {
            return ColumnType.Boolean;
        }

        if (tokenType == JsonTokenType.StartObject)
        {
            return ColumnType.JsonObject;
        }

        if (tokenType == JsonTokenType.StartArray)
        {
            return ColumnType.JsonArray;
        }

        return ColumnType.Text;
    }

    /// <summary>
    /// Returns true if the token type is <see cref="JsonTokenType.Null"/>.
    /// Mirrors the role of <c>TypeInferrer.IsEmptyOrWhitespace()</c> in the CSV pipeline.
    /// </summary>
    /// <param name="tokenType">The current JSON token type.</param>
    public static bool IsNullToken(JsonTokenType tokenType) =>
        tokenType == JsonTokenType.Null;
}
