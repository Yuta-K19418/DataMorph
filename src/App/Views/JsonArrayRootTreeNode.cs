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
        throw new NotImplementedException();
    }

    private static ITreeNode CreateElementNode(ReadOnlyMemory<byte> bytes, int index)
    {
        throw new NotImplementedException();
    }
}
