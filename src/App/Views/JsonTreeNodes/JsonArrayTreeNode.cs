using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonTreeNodes;

/// <summary>
/// Represents a JSON array node in the tree.
/// Supports lazy loading of children on first access.
/// </summary>
internal sealed class JsonArrayTreeNode : TreeNode
{
    private readonly ReadOnlyMemory<byte> _rawJson;
    private bool _childrenLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArrayTreeNode"/> class.
    /// </summary>
    /// <param name="rawJson">The raw JSON bytes representing this array.</param>
    public JsonArrayTreeNode(ReadOnlyMemory<byte> rawJson)
    {
        _rawJson = rawJson;
        Text = FormatDisplayText();
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

    private string FormatDisplayText()
    {
        throw new NotImplementedException();
    }
}
