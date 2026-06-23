using System.Text.Json;
using DataMorph.Engine.IO.Json;
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
    /// <param name="recordPosition">Ancestor root record position to propagate to child nodes.</param>
    /// <returns>A tree node representing the current token, or null if unrecognized.</returns>
    internal static ITreeNode? CreateChildNode(
        ref Utf8JsonReader reader,
        string label,
        JsonRawBytes rawJson,
        long? recordPosition = null
    )
    {
        return reader.TokenType switch
        {
            JsonTokenType.StartObject => CreateNestedObjectNode(ref reader, label, rawJson, recordPosition),
            JsonTokenType.StartArray => CreateNestedArrayNode(ref reader, label, rawJson, recordPosition),
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
        JsonRawBytes rawJson,
        long? recordPosition
    )
    {
        var objectBytes = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);
        return new JsonObjectTreeNode(objectBytes, $"{label}: ")
        {
            KeyName = label,
            RecordPosition = recordPosition,
        };
    }

    private static JsonArrayTreeNode CreateNestedArrayNode(
        ref Utf8JsonReader reader,
        string label,
        JsonRawBytes rawJson,
        long? recordPosition
    )
    {
        var arrayBytes = JsonByteExtractor.ExtractNestedBytes(ref reader, rawJson);
        return new JsonArrayTreeNode(arrayBytes, $"{label}: ")
        {
            KeyName = label,
            RecordPosition = recordPosition,
        };
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
