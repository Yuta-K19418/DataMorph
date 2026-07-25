using System.Text.Json;

namespace Refedle.Engine.IO.Json;

/// <summary>
/// Shared Engine-layer utility for JSON traversal primitives: extracting the raw bytes of a
/// nested value, counting an Object's top-level properties or an Array's top-level elements,
/// and formatting collapsed preview text for both. Extracted to a common location so the
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
    public static JsonRawBytes ExtractNestedBytes(
        ref Utf8JsonReader reader,
        JsonRawBytes rawJson)
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

    /// <summary>
    /// Counts the top-level properties of a JSON object by tracking brace/bracket depth.
    /// The reader must be positioned at a <see cref="JsonTokenType.StartObject"/> token; on return
    /// the reader has consumed the entire object, ending at its matching EndObject token.
    /// </summary>
    public static int CountObjectProperties(ref Utf8JsonReader reader)
    {
        var propertyCount = 0;
        var depth = 1;

        while (depth > 0 && reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                depth++;
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
            }

            if (depth == 1 && reader.TokenType == JsonTokenType.PropertyName)
            {
                propertyCount++;
            }
        }

        return propertyCount;
    }

    /// <summary>
    /// Counts the top-level elements of a JSON array by tracking bracket/brace depth.
    /// The reader must be positioned at a <see cref="JsonTokenType.StartArray"/> token; on return
    /// the reader has consumed the entire array, ending at its matching EndArray token.
    /// </summary>
    public static int CountArrayElements(ref Utf8JsonReader reader)
    {
        var elementCount = 0;
        var depth = 1;

        while (depth > 0 && reader.Read())
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                depth++;
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
            }

            if (depth == 1)
            {
                elementCount++;
            }
        }

        return elementCount;
    }

    /// <summary>
    /// Formats a JSON object's collapsed preview text (e.g. "{Object: 3 properties}"). The reader
    /// must be positioned at a <see cref="JsonTokenType.StartObject"/> token.
    /// </summary>
    public static string FormatObjectPreview(ref Utf8JsonReader reader)
    {
        var propertyCount = CountObjectProperties(ref reader);
        return FormattableString.Invariant($"{{Object: {propertyCount:N0} properties}}");
    }

    /// <summary>
    /// Formats a JSON array's collapsed preview text (e.g. "[Array: 3 items]"). The reader must be
    /// positioned at a <see cref="JsonTokenType.StartArray"/> token.
    /// </summary>
    public static string FormatArrayPreview(ref Utf8JsonReader reader)
    {
        var elementCount = CountArrayElements(ref reader);
        return FormattableString.Invariant($"[Array: {elementCount:N0} items]");
    }
}
