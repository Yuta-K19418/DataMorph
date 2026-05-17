using System.Text.Json;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// The single root node of the JSON Array explorer tree. Displays "[ n items ]".
/// On first <see cref="Children"/> access (lazy), reads element bytes from
/// <see cref="ElementByteCache"/> and constructs child element nodes.
/// </summary>
internal sealed class JsonArrayRootTreeNode : TreeNode
{
    internal const int MaxElementsShown = 5_000;

    private readonly ElementByteCache _cache;
    private bool _childrenLoaded;

    public JsonArrayRootTreeNode(ElementByteCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        Text = $"[ {cache.TotalRows} items ]";
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
        var totalToShow = Math.Min(_cache.TotalRows, MaxElementsShown);
        List<ITreeNode> children = [];
        children.EnsureCapacity(totalToShow + 1);

        for (var i = 0; i < totalToShow; i++)
        {
            var bytes = _cache.GetRow(i);
            if (bytes.IsEmpty)
            {
                continue;
            }

            children.Add(CreateElementNode(bytes, i));
        }

        if (_cache.TotalRows > MaxElementsShown)
        {
            var overflow = _cache.TotalRows - MaxElementsShown;
            var elementText = overflow == 1 ? "element" : "elements";
            children.Add(new JsonValueTreeNode(
                $"... ({overflow} more {elementText} - use a filtered view)")
            {
                ValueKind = JsonValueKind.Undefined,
            });
        }

        base.Children = children;
    }

    private static ITreeNode CreateElementNode(ReadOnlyMemory<byte> bytes, int index)
    {
        var prefix = $"[{index}]: ";
        var reader = new Utf8JsonReader(bytes.Span);

        if (!reader.Read())
        {
            return new JsonValueTreeNode($"{prefix}[Invalid JSON]")
            {
                ValueKind = JsonValueKind.Undefined,
            };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var node = new JsonObjectTreeNode(bytes);
            node.Text = $"{prefix}{node.Text}";
            return node;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var node = new JsonArrayTreeNode(bytes);
            node.Text = $"{prefix}{node.Text}";
            return node;
        }

        return new JsonValueTreeNode($"{prefix}{reader.GetPrimitiveDisplay()}")
        {
            ValueKind = reader.TokenType.ToJsonValueKind(),
        };
    }
}
