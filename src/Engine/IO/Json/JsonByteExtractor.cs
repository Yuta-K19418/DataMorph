using System.Text.Json;

namespace DataMorph.Engine.IO.Json;

/// <summary>
/// Shared Engine-layer utility for extracting the raw bytes of a nested JSON value
/// (Object or Array) by tracking brace/bracket depth. Extracted to a common location so the
/// App-layer tree node helper can reuse it without duplicating the depth-tracking logic.
/// </summary>
public static class JsonByteExtractor
{
    /// <summary>
    /// Advances <paramref name="reader"/> past the current nested value (Object or Array) and
    /// returns a slice of <paramref name="rawJson"/> covering it exactly.
    /// The reader must be positioned at a <see cref="JsonTokenType.StartObject"/> or
    /// <see cref="JsonTokenType.StartArray"/> token.
    /// </summary>
    /// <param name="reader">The JSON reader positioned at a StartObject or StartArray token.</param>
    /// <param name="rawJson">The full raw JSON bytes containing the nested structure.</param>
    /// <returns>A slice of the raw bytes covering exactly the nested structure.</returns>
    public static ReadOnlyMemory<byte> ExtractNestedBytes(
        ref Utf8JsonReader reader,
        ReadOnlyMemory<byte> rawJson)
    {
        var startPosition = (int)reader.TokenStartIndex;
        var depth = 1;

        while (depth > 0 && reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                depth++;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
            }
        }

        var endPosition = (int)reader.TokenStartIndex + 1;
        return rawJson.Slice(startPosition, endPosition - startPosition);
    }
}
