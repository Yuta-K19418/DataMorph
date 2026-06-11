using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonRangeTreeNodes;

/// <summary>
/// Represents a range within a large JSON Lines file.
/// When <c>_count &gt; <see cref="RangeTreeNodeBase.RangeSize"/></c>, children are nested
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
    /// Exposes <see cref="RangeTreeNodeBase.GetNodeGroupSize"/> for external callers
    /// such as <see cref="JsonLinesTreeView"/>.
    /// </summary>
    internal static new long GetNodeGroupSize(long fileSize) =>
        RangeTreeNodeBase.GetNodeGroupSize(fileSize);

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
                return new JsonObjectTreeNode(lineBytes, prefix)
                {
                    LineNumber = (int)(lineIndex + 1),
                };
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
        throw new NotImplementedException();
    }
}
