using System.Text.Json;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonTreeNodes;

/// <summary>
/// Represents a JSON primitive value (string, number, boolean, null).
/// Has no children.
/// </summary>
internal sealed class JsonValueTreeNode : TreeNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonValueTreeNode"/> class.
    /// </summary>
    /// <param name="text">The display text for this value node.</param>
    public JsonValueTreeNode(string text)
        : base(text)
    {
        Children = [];
    }

    /// <summary>
    /// Gets or initializes the JSON value kind of this node.
    /// </summary>
    public JsonValueKind ValueKind { get; init; }
}
