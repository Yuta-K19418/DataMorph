using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonRangeTreeNodes;

/// <summary>
/// Represents a range within a large JSON Array.
/// When <c>_count &gt; <see cref="RangeTreeNodeBase.RangeSize"/></c>, children are nested
/// <see cref="JsonArrayRangeTreeNode"/> instances; otherwise, children are individual element nodes.
/// Children are loaded on demand via <see cref="RangeTreeNodeBase.EnsureChildrenLoaded"/>.
/// </summary>
internal sealed class JsonArrayRangeTreeNode : RangeTreeNodeBase
{
    private readonly IRowIndexer _indexer;
    private readonly ElementReader _reader;
    private readonly long _startIndex;
    private readonly long _count;

    internal JsonArrayRangeTreeNode(IRowIndexer indexer, ElementReader reader, long startIndex, long count)
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
    /// Exposes <see cref="RangeTreeNodeBase.GetNodeGroupSize"/> for external callers
    /// such as <see cref="JsonArrayTreeView"/>.
    /// </summary>
    internal static new long GetNodeGroupSize(long fileSize) =>
        RangeTreeNodeBase.GetNodeGroupSize(fileSize);

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

    /// <inheritdoc/>
    /// <remarks>
    /// When <c>_count &gt; RangeSize</c>, creates nested <see cref="JsonArrayRangeTreeNode"/> children;
    /// otherwise, reads element bytes and creates individual element nodes.
    /// </remarks>
    protected override void LoadChildren()
    {
        throw new NotImplementedException();
    }
}
