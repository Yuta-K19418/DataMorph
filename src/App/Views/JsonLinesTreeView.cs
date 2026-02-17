using System.Globalization;
using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// A TreeView that displays JSON Lines data as a hierarchical tree.
/// Creates TreeNode instances from raw JSON bytes provided by the Engine layer.
/// </summary>
internal sealed class JsonLinesTreeView : TreeView
{
    private const int InitialLoadCount = 50;

    private readonly JsonLineByteCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLinesTreeView"/> class.
    /// </summary>
    /// <param name="indexer">The row indexer for the JSON Lines file.</param>
    public JsonLinesTreeView(RowIndexer indexer)
    {
        _cache = new JsonLineByteCache(indexer);
        LoadInitialRootNodes();
    }

    private void LoadInitialRootNodes()
    {
        var linesToLoad = Math.Min(_cache.TotalLines, InitialLoadCount);

        for (var i = 0; i < linesToLoad; i++)
        {
            var lineBytes = _cache.GetLineBytes(i);
            if (lineBytes.IsEmpty)
            {
                continue;
            }

            var rootNode = CreateRootNode(lineBytes, i);
            AddObject(rootNode);
        }
    }

    private static ITreeNode CreateRootNode(ReadOnlyMemory<byte> lineBytes, int lineIndex)
    {
        var lineNumber = lineIndex + 1;
        var reader = new Utf8JsonReader(lineBytes.Span);

        if (!reader.Read())
        {
            return new JsonValueTreeNode($"Line {lineNumber}: [Invalid JSON]")
            {
                ValueKind = JsonValueKind.Undefined,
            };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return new JsonObjectTreeNode(lineBytes)
            {
                LineNumber = lineNumber,
                Text = $"Line {lineNumber}: {{...}}",
            };
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return new JsonArrayTreeNode(lineBytes) { Text = $"Line {lineNumber}: [...]" };
        }

        return new JsonValueTreeNode($"Line {lineNumber}: {GetPrimitiveDisplay(ref reader)}")
        {
            ValueKind = GetValueKind(reader.TokenType),
        };
    }

    private static string GetPrimitiveDisplay(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String =>
                $"\"{JsonTreeNodeHelper.EscapeString(reader.GetString() ?? string.Empty)}\"",
            JsonTokenType.Number when reader.TryGetDecimal(out var d) => d.ToString(
                CultureInfo.InvariantCulture
            ),
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => "<null>",
            _ => "<unknown>",
        };
    }

    private static JsonValueKind GetValueKind(JsonTokenType tokenType)
    {
        return tokenType switch
        {
            JsonTokenType.String => JsonValueKind.String,
            JsonTokenType.Number => JsonValueKind.Number,
            JsonTokenType.True => JsonValueKind.True,
            JsonTokenType.False => JsonValueKind.False,
            JsonTokenType.Null => JsonValueKind.Null,
            _ => JsonValueKind.Undefined,
        };
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cache.Dispose();
        }

        base.Dispose(disposing);
    }
}
