using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonRangeTreeNodes;

/// <summary>
/// Represents a range within a large JSON Lines file.
/// When <c>_count &gt; <see cref="RangePartitionPolicy.RangeSize"/></c>, children are nested
/// <see cref="JsonLinesRangeTreeNode"/> instances; otherwise, children are individual line nodes.
/// Children are loaded on demand via <see cref="RangeTreeNodeBase.EnsureChildrenLoaded"/>.
/// </summary>
internal sealed class JsonLinesRangeTreeNode : RangeTreeNodeBase
{
    private readonly IRowIndexer _indexer;
    private readonly RowReader _reader;
    private readonly long _startIndex;
    private readonly long _count;

    internal JsonLinesRangeTreeNode(IRowIndexer indexer, RowReader reader, long startIndex, long count)
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
    /// Creates a line node for display from raw JSON bytes.
    /// <paramref name="lineIndex"/> is 0-based; the display label uses <c>lineNumber = lineIndex + 1</c> (1-based).
    /// </summary>
    internal static ITreeNode CreateLineNode(ReadOnlyMemory<byte> lineBytes, long lineIndex)
    {
        var prefix = $"Line {lineIndex + 1}: ";
        ITreeNode invalidNode() =>
            new JsonValueTreeNode($"{prefix}[Invalid JSON]") { ValueKind = JsonValueKind.Undefined };

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
                return new JsonObjectTreeNode(lineBytes, prefix);
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                return new JsonArrayTreeNode(lineBytes, prefix);
            }

            return new JsonValueTreeNode($"{prefix}{reader.GetPrimitiveDisplay()}")
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
    /// <remarks>
    /// When <c>_count &gt; RangeSize</c>, creates nested <see cref="JsonLinesRangeTreeNode"/> children;
    /// otherwise, reads line bytes and creates individual line nodes.
    /// </remarks>
    protected override void LoadChildren()
    {
        if (_count > RangePartitionPolicy.RangeSize)
        {
            AddNestedRangeNodes();
            return;
        }

        AddDirectChildren();
    }

    private void AddNestedRangeNodes()
    {
        var childStart = _startIndex;
        var remaining = _count;
        List<ITreeNode> children = [];

        while (remaining > 0)
        {
            var childCount = Math.Min(remaining, RangePartitionPolicy.RangeSize);
            children.Add(new JsonLinesRangeTreeNode(_indexer, _reader, childStart, childCount));
            childStart += childCount;
            remaining -= childCount;
        }

        Children = children;
    }

    private void AddDirectChildren()
    {
        var (byteOffset, rowOffset) = _indexer.GetCheckPoint(_startIndex);
        var lines = _reader.ReadLineBytes(byteOffset, rowOffset, (int)_count);
        List<ITreeNode> children = [];

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsEmpty)
            {
                continue;
            }

            children.Add(CreateLineNode(lines[i], _startIndex + i));
        }

        Children = children;
    }
}
