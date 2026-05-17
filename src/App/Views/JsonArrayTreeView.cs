using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;

namespace DataMorph.App.Views;

/// <summary>
/// <see cref="MorphTreeView"/> subclass for JSON Array files.
/// Creates <see cref="ElementByteCache"/> and a single <see cref="JsonArrayRootTreeNode"/>.
/// Inherits all Vim-key handling and 't'-key table-mode toggle from <see cref="MorphTreeView"/>.
/// </summary>
internal sealed class JsonArrayTreeView : MorphTreeView
{
    private readonly ElementByteCache _cache;

    public JsonArrayTreeView(IRowIndexer indexer, Action onTableModeToggle)
        : base(onTableModeToggle)
    {
        _cache = new ElementByteCache(indexer);
        var rootNode = new JsonArrayRootTreeNode(_cache);
        AddObject(rootNode);
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
