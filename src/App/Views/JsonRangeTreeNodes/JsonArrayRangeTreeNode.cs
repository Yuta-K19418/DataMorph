using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonRangeTreeNodes;

/// <summary>
/// Represents a range within a large JSON Array.
/// When <c>Count &gt; <see cref="RangePartitionPolicy.RangeSize"/></c>, children are nested
/// <see cref="JsonArrayRangeTreeNode"/> instances; otherwise, children are individual element nodes.
/// Children are loaded on demand via <see cref="RangeTreeNodeBase.EnsureChildrenLoaded"/>.
/// </summary>
internal sealed class JsonArrayRangeTreeNode : RangeTreeNodeBase
{
    private readonly IRowIndexer _indexer;
    private readonly ElementReader _reader;

    internal JsonArrayRangeTreeNode(IRowIndexer indexer, ElementReader reader, long startIndex, long count)
        : base(startIndex, count)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(reader);
        _indexer = indexer;
        _reader = reader;
        Text = count == 0
            ? $"[{startIndex} - (empty)]"
            : $"[{startIndex} - {startIndex + count - 1}]";
    }

    /// <summary>
    /// Creates an element node for display from raw JSON bytes.
    /// </summary>
    internal static ITreeNode CreateElementNode(ReadOnlyMemory<byte> bytes, long index)
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

            return new JsonValueTreeNode(withIndex(reader.GetPrimitiveDisplay()))
            {
                ValueKind = reader.TokenType.ToJsonValueKind(),
            };
        }
        catch (JsonException)
        {
            return invalidNode();
        }
    }

    /// <inheritdoc/>
    protected override RangeTreeNodeBase CreateRangeNode(long startIndex, long count) =>
        new JsonArrayRangeTreeNode(_indexer, _reader, startIndex, count);

    /// <inheritdoc/>
    /// <remarks>
    /// Reads element bytes and creates individual element nodes for the current range.
    /// </remarks>
    protected override void AddDirectChildren()
    {
        var (byteOffset, rowOffset) = _indexer.GetCheckPoint(StartIndex);
        var elements = _reader.ReadElementBytes(byteOffset, rowOffset, (int)Count);
        List<ITreeNode> children = [];

        for (var i = 0; i < elements.Count; i++)
        {
            if (elements[i].IsEmpty)
            {
                continue;
            }

            children.Add(CreateElementNode(elements[i], StartIndex + i));
        }

        Children = children;
    }
}
