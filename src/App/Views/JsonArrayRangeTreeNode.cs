using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Represents a 1,000-item range within a large JSON Array.
/// On first <see cref="Children"/> access (lazy), reads element bytes from
/// <see cref="ElementByteCache"/> and constructs child element nodes for that range.
/// </summary>
internal sealed class JsonArrayRangeTreeNode : TreeNode
{
    private readonly ElementByteCache _cache;
    private readonly int _startIndex;
    private readonly int _count;
    private bool _childrenLoaded;

    public JsonArrayRangeTreeNode(ElementByteCache cache, int startIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        _cache = cache;
        _startIndex = startIndex;
        _count = count;
        Text = count == 0
            ? $"[{startIndex} - (empty)]"
            : $"[{startIndex} - {startIndex + count - 1}]";
    }

    /// <inheritdoc/>
    public override IList<ITreeNode> Children
    {
        get
        {
            if (!_childrenLoaded)
            {
                LoadChildren();
                _childrenLoaded = true;
            }
            return base.Children;
        }
        set => base.Children = value;
    }

    private void LoadChildren()
    {
        List<ITreeNode> children = [];

        for (var i = 0; i < _count; i++)
        {
            var bytes = _cache.GetRow(_startIndex + i);
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
