using System.Text.Json;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonTreeNodes;

/// <summary>
/// Shared utilities for JSON tree node parsing and display.
/// </summary>
internal static class JsonTreeNodeHelper
{
    /// <summary>
    /// Creates a child tree node from the current JSON token.
    /// </summary>
    /// <param name="reader">The JSON reader positioned at a value token.</param>
    /// <param name="label">The display label prefix (e.g. "name" or "[0]").</param>
    /// <param name="rawJson">The raw JSON bytes for extracting nested structures.</param>
    /// <returns>A tree node representing the current token, or null if unrecognized.</returns>
    internal static ITreeNode? CreateChildNode(
        ref Utf8JsonReader reader,
        string label,
        ReadOnlyMemory<byte> rawJson
    )
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartObject => CreateNestedObjectNode(ref reader, label, rawJson),
            JsonTokenType.StartArray => CreateNestedArrayNode(ref reader, label, rawJson),
            JsonTokenType.String => new JsonValueTreeNode(
                $"{label}: \"{EscapeString(reader.GetString() ?? string.Empty)}\""
            )
            {
                ValueKind = JsonValueKind.String,
            },
            JsonTokenType.Number => CreateNumberNode(ref reader, label),
            JsonTokenType.True => new JsonValueTreeNode($"{label}: true")
            {
                ValueKind = JsonValueKind.True,
            },
            JsonTokenType.False => new JsonValueTreeNode($"{label}: false")
            {
                ValueKind = JsonValueKind.False,
            },
            JsonTokenType.Null => new JsonValueTreeNode($"{label}: <null>")
            {
                ValueKind = JsonValueKind.Null,
            },
            _ => new JsonValueTreeNode($"{label}: <unknown>")
            {
                ValueKind = JsonValueKind.Undefined,
            },
        };
    }

    /// <summary>
    /// Extracts the raw bytes of a nested JSON structure (object or array)
    /// by tracking brace/bracket depth.
    /// </summary>
    /// <param name="reader">The JSON reader positioned at a StartObject or StartArray token.</param>
    /// <param name="rawJson">The full raw JSON bytes.</param>
    /// <returns>A slice of the raw bytes covering the nested structure.</returns>
    internal static ReadOnlyMemory<byte> ExtractNestedBytes(
        ref Utf8JsonReader reader,
        ReadOnlyMemory<byte> rawJson
    )
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
    /// Escapes special characters in a string for tree node display.
    /// </summary>
    internal static string EscapeString(string value)
    {
        return value
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static JsonObjectTreeNode CreateNestedObjectNode(
        ref Utf8JsonReader reader,
        string label,
        ReadOnlyMemory<byte> rawJson
    )
    {
        var objectBytes = ExtractNestedBytes(ref reader, rawJson);
        return new JsonObjectTreeNode(objectBytes) { Text = $"{label}: {{...}}" };
    }

    private static JsonArrayTreeNode CreateNestedArrayNode(
        ref Utf8JsonReader reader,
        string label,
        ReadOnlyMemory<byte> rawJson
    )
    {
        var arrayBytes = ExtractNestedBytes(ref reader, rawJson);
        return new JsonArrayTreeNode(arrayBytes) { Text = $"{label}: [...]" };
    }

    private static JsonValueTreeNode CreateNumberNode(ref Utf8JsonReader reader, string label)
    {
        if (reader.TryGetDecimal(out var decimalValue))
        {
            return new JsonValueTreeNode($"{label}: {decimalValue}")
            {
                ValueKind = JsonValueKind.Number,
            };
        }

        var doubleValue = reader.GetDouble();
        return new JsonValueTreeNode($"{label}: {doubleValue}")
        {
            ValueKind = JsonValueKind.Number,
        };
    }
}
