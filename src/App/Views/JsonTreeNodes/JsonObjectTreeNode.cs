using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonTreeNodes;

/// <summary>
/// Represents a JSON object node in the tree.
/// Supports lazy loading of children on first access.
/// </summary>
internal sealed class JsonObjectTreeNode : TreeNode
{
    private readonly ReadOnlyMemory<byte> _rawJson;
    private bool _childrenLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonObjectTreeNode"/> class.
    /// </summary>
    /// <param name="rawJson">The raw JSON bytes representing this object.</param>
    public JsonObjectTreeNode(ReadOnlyMemory<byte> rawJson)
    {
        _rawJson = rawJson;
        Text = FormatDisplayText();
    }

    /// <summary>
    /// Gets or initializes the line number of this root-level JSON object.
    /// </summary>
    public int? LineNumber { get; init; }

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
