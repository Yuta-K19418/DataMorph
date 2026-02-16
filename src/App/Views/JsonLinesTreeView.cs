using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// A TreeView that displays JSON Lines data as a hierarchical tree.
/// Creates TreeNode instances from raw JSON bytes provided by the Engine layer.
/// </summary>
internal sealed class JsonLinesTreeView : TreeView
{
    private readonly JsonLineByteCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLinesTreeView"/> class.
    /// </summary>
    /// <param name="indexer">The row indexer for the JSON Lines file.</param>
    public JsonLinesTreeView(RowIndexer indexer)
    {
        _cache = new JsonLineByteCache(indexer);
        LoadInitialRootNodes();
    }

    private void LoadInitialRootNodes()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cache.Dispose();
        }

        base.Dispose(disposing);
    }
}
