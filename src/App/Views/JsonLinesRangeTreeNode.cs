using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Represents a 1,000-line range within a large JSON Lines file.
/// Children are loaded on demand via <see cref="EnsureChildrenLoaded"/>,
/// called by <see cref="DelegateTreeBuilder{T}"/> on first expansion.
/// </summary>
internal sealed class JsonLinesRangeTreeNode : TreeNode
{
    private readonly IRowIndexer _indexer;
    private readonly RowReader _reader;
    private readonly int _startIndex;
    private readonly int _count;

    public JsonLinesRangeTreeNode(IRowIndexer indexer, RowReader reader, int startIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        _indexer = indexer;
        _reader = reader;
        _startIndex = startIndex;
        _count = count;
        Text = count == 0
            ? $"Lines {startIndex + 1} (empty)"
            : $"Lines {startIndex + 1}-{startIndex + count}";
    }

    /// <summary>
    /// Whether children have been loaded at least once.
    /// Used by <see cref="DelegateTreeBuilder{ITreeNode}"/> to determine expand-arrow visibility
    /// without touching <see cref="Children"/> (which would trigger premature loading).
    /// </summary>
    internal bool IsChildrenLoaded { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// No lazy loading — <see cref="DelegateTreeBuilder{ITreeNode}"/> controls load timing
    /// via <see cref="EnsureChildrenLoaded"/> to prevent <c>TreeView.AddObject()</c> from
    /// triggering eager child retrieval.
    /// </remarks>
    public override IList<ITreeNode> Children => base.Children;

    /// <summary>
    /// Loads children from the reader if not already loaded.
    /// Called by the <see cref="DelegateTreeBuilder{ITreeNode}"/> child getter on expansion.
    /// </summary>
    internal void EnsureChildrenLoaded()
    {
        if (IsChildrenLoaded)
        {
            return;
        }

        LoadChildren();
        IsChildrenLoaded = true;
    }

    private void LoadChildren()
    {
        var (byteOffset, rowOffset) = _indexer.GetCheckPoint(_startIndex);
        var allBytes = _reader.ReadLineBytes(byteOffset, rowOffset, _count);
        List<ITreeNode> children = [];

        for (var i = 0; i < allBytes.Count; i++)
        {
            var bytes = allBytes[i];
            if (bytes.IsEmpty)
            {
                continue;
            }

            children.Add(CreateLineNode(bytes, _startIndex + i));
        }

        base.Children = children;
    }

    /// <summary>
    /// Creates a line node for display from raw JSON bytes.
    /// <paramref name="lineIndex"/> is 0-based; the display label uses <c>lineNumber = lineIndex + 1</c> (1-based).
    /// </summary>
    internal static ITreeNode CreateLineNode(ReadOnlyMemory<byte> lineBytes, int lineIndex)
    {
        var lineNumber = lineIndex + 1;
        string withLine(string text) => $"Line {lineNumber}: {text}";
        ITreeNode invalidNode() =>
            new JsonValueTreeNode(withLine("[Invalid JSON]")) { ValueKind = JsonValueKind.Undefined };

        if (lineBytes.IsEmpty)
        {
            return invalidNode();
        }

        var reader = new Utf8JsonReader(lineBytes.Span);

        try
        {
            if (!reader.Read())
            {
                return invalidNode();
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return new JsonObjectTreeNode(lineBytes)
                {
                    LineNumber = lineNumber,
                    Text = withLine("{...}"),
                };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return new JsonArrayTreeNode(lineBytes)
                {
                    Text = withLine("[...]"),
                };
            }
        }
        catch (JsonException)
        {
            return invalidNode();
        }

        return new JsonValueTreeNode(withLine(reader.GetPrimitiveDisplay()))
        {
            ValueKind = reader.TokenType.ToJsonValueKind(),
        };
    }
}
