using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Represents a 1,000-item range within a large JSON Array.
/// Children are loaded on demand via <see cref="EnsureChildrenLoaded"/>,
/// called by <see cref="DelegateTreeBuilder{ITreeNode}"/> on first expansion.
/// </summary>
internal sealed class JsonArrayRangeTreeNode : TreeNode
{
    private readonly IRowIndexer _indexer;
    private readonly ElementReader _reader;
    private readonly int _startIndex;
    private readonly int _count;

    public JsonArrayRangeTreeNode(IRowIndexer indexer, ElementReader reader, int startIndex, int count)
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
            ? $"[{startIndex} - (empty)]"
            : $"[{startIndex} - {startIndex + count - 1}]";
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
        var allBytes = _reader.ReadElementBytes(byteOffset, rowOffset, _count);
        List<ITreeNode> children = [];

        for (var i = 0; i < allBytes.Count; i++)
        {
            var bytes = allBytes[i];
            if (bytes.IsEmpty)
            {
                continue;
            }

            children.Add(CreateElementNode(bytes, _startIndex + i));
        }

        base.Children = children;
    }

    internal static ITreeNode CreateElementNode(ReadOnlyMemory<byte> bytes, int index)
    {
        string withIndex(string text) => $"[{index}]: {text}";
        ITreeNode invalidNode() =>
            new JsonValueTreeNode(withIndex("[Invalid JSON]")) { ValueKind = JsonValueKind.Undefined };

        if (bytes.IsEmpty)
        {
            return invalidNode();
        }

        var reader = new Utf8JsonReader(bytes.Span);

        try
        {
            if (!reader.Read())
            {
                return invalidNode();
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var node = new JsonObjectTreeNode(bytes);
                node.Text = withIndex(node.Text);
                return node;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var node = new JsonArrayTreeNode(bytes);
                node.Text = withIndex(node.Text);
                return node;
            }
        }
        catch (JsonException)
        {
            return invalidNode();
        }

        return new JsonValueTreeNode(withIndex(reader.GetPrimitiveDisplay()))
        {
            ValueKind = reader.TokenType.ToJsonValueKind(),
        };
    }
}
