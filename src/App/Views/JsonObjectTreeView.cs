using DataMorph.App.Views.JsonTreeNodes;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// <see cref="MorphTreeView"/> subclass for JSON Object files.
/// Creates one root-level tree node per top-level key from <see cref="Engine.IO.JsonObject.TopLevelScanner"/> results.
/// </summary>
internal sealed class JsonObjectTreeView : MorphTreeView
{
    private JsonObjectTreeView(Action onTableModeToggle)
        : base(onTableModeToggle) { }

    /// <summary>
    /// Creates a new <see cref="JsonObjectTreeView"/> populated with root-level key nodes.
    /// </summary>
    /// <param name="entries">
    /// The key-value pairs returned by <see cref="Engine.IO.JsonObject.TopLevelScanner.Scan"/>.
    /// </param>
    /// <param name="onTableModeToggle">
    /// Callback invoked when the user presses 't' to toggle between tree and table mode.
    /// JSON Object does not support table mode, so callers pass a no-op.
    /// </param>
    /// <returns>A populated <see cref="JsonObjectTreeView"/>.</returns>
    internal static JsonObjectTreeView Create(
        IReadOnlyList<(string key, ReadOnlyMemory<byte> value)> entries,
        Action onTableModeToggle)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a single tree node for a top-level key-value pair.
    /// Mirrors <see cref="JsonArrayRangeTreeNode.CreateElementNode"/>:
    /// reads the first token from <paramref name="valueBytes"/> and dispatches to the
    /// appropriate node type (<see cref="JsonObjectTreeNode"/>,
    /// <see cref="JsonArrayTreeNode"/>, or <see cref="JsonValueTreeNode"/>),
    /// then prepends <c>"{key}: "</c> to the node's display text.
    /// </summary>
    /// <param name="key">The top-level property key.</param>
    /// <param name="valueBytes">The raw JSON bytes of the property value.</param>
    /// <returns>A tree node representing the key-value pair.</returns>
    internal static ITreeNode CreateKeyNode(string key, ReadOnlyMemory<byte> valueBytes)
    {
        throw new NotImplementedException();
    }
}
