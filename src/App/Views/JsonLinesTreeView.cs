using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// A <see cref="MorphTreeView"/> that displays JSON Lines data as a hierarchical tree.
/// Creates TreeNode instances from raw JSON bytes provided by the Engine layer.
/// </summary>
internal sealed class JsonLinesTreeView : MorphTreeView
{
    private const int InitialLoadCount = 50;

    private readonly RowByteCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLinesTreeView"/> class.
    /// </summary>
    /// <param name="indexer">The row indexer for the JSON Lines file.</param>
    /// <param name="onTableModeToggle">Callback invoked when the user presses 't'.</param>
    public JsonLinesTreeView(IRowIndexer indexer, Action onTableModeToggle)
        : base(onTableModeToggle)
    {
        _cache = new RowByteCache(indexer);
        LoadInitialRootNodes();
    }

    private void LoadInitialRootNodes()
    {
        var linesToLoad = Math.Min(_cache.TotalRows, InitialLoadCount);

        for (var i = 0; i < linesToLoad; i++)
        {
            var lineBytes = _cache.GetRow(i);
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

        return new JsonValueTreeNode($"Line {lineNumber}: {reader.GetPrimitiveDisplay()}")
        {
            ValueKind = reader.TokenType.ToJsonValueKind(),
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
