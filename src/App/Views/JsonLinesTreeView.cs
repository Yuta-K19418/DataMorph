using DataMorph.App.Views.JsonRangeTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.App.Views;

/// <summary>
/// A <see cref="RangeTreeViewBase"/> that displays JSON Lines data as a hierarchical tree.
/// Creates TreeNode instances from raw JSON bytes provided by the Engine layer.
/// Supports lazy loading with progressive node addition for large files.
/// </summary>
internal sealed class JsonLinesTreeView : RangeTreeViewBase
{
    private readonly RowReader _reader;

    private JsonLinesTreeView(
        IRowIndexer indexer,
        RowReader reader,
        Action onTableModeToggle,
        Action<Action> uiThreadInvoke,
        long nodeGroupSize)
        : base(indexer, onTableModeToggle, uiThreadInvoke, nodeGroupSize)
    {
        _reader = reader;
    }

    /// <summary>
    /// Factory method that creates a <see cref="JsonLinesTreeView"/> for the given JSON Lines file.
    /// </summary>
    /// <param name="indexer">The row indexer for the JSON Lines file.</param>
    /// <param name="onTableModeToggle">Callback invoked when the user presses 't'.</param>
    /// <param name="uiThreadInvoke">Marshals actions to the UI thread for thread-safe <c>AddObject</c> calls.</param>
    /// <returns>A new <see cref="JsonLinesTreeView"/> instance populated with line or range nodes.</returns>
    internal static JsonLinesTreeView Create(
        IRowIndexer indexer,
        Action onTableModeToggle,
        Action<Action> uiThreadInvoke)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        ArgumentNullException.ThrowIfNull(uiThreadInvoke);

        var nodeGroupSize = RangePartitionPolicy.GetNodeGroupSize(indexer.FileSize);
        var view = new JsonLinesTreeView(
            indexer, new RowReader(indexer.FilePath), onTableModeToggle, uiThreadInvoke, nodeGroupSize);

        if (indexer.IsIndexingCompleted)
        {
            view.BuildInitialNodes(indexer.TotalRows);
            return view;
        }

        view.StartProgressiveLoading();

        return view;
    }

    /// <inheritdoc/>
    protected override RangeTreeNodeBase CreateRangeNode(long startIndex, long count) =>
        new JsonLinesRangeTreeNode(Indexer, _reader, startIndex, count);

    /// <inheritdoc/>
    protected override void AddSmallFileNodes(long totalRows)
    {
        var (byteOffset, rowOffset) = Indexer.GetCheckPoint(0);
        var lines = _reader.ReadLineBytes(byteOffset, rowOffset, (int)totalRows);

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].IsEmpty)
            {
                continue;
            }

            AddObject(JsonLinesRangeTreeNode.CreateLineNode(lines[i], i));
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _reader.Dispose();
        }

        base.Dispose(disposing);
    }
}
