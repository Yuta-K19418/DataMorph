using DataMorph.App.Views.JsonRangeTreeNodes;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// A <see cref="MorphTreeView"/> that displays JSON Lines data as a hierarchical tree.
/// Creates TreeNode instances from raw JSON bytes provided by the Engine layer.
/// Supports lazy loading with progressive node addition for large files.
/// </summary>
internal sealed class JsonLinesTreeView : MorphTreeView
{
    private readonly IRowIndexer _indexer;
    private readonly RowReader _reader;
    private readonly Action<Action> _uiThreadInvoke;
    private readonly long _nodeGroupSize;
    private readonly Action<long, long> _progressHandler;
    private readonly Action _completedHandler;
#pragma warning disable CA1823, CS0169, IDE0044 // Interlocked counter fields; assigned via Interlocked.Exchange in event handlers
    private long _addedSuperRangeNodeCount;
    private int _finalHandled;
#pragma warning restore CA1823, CS0169, IDE0044

    private JsonLinesTreeView(
        IRowIndexer indexer,
        RowReader reader,
        Action onTableModeToggle,
        Action<Action> uiThreadInvoke,
        long nodeGroupSize)
        : base(onTableModeToggle)
    {
        _indexer = indexer;
        _reader = reader;
        _uiThreadInvoke = uiThreadInvoke;
        _nodeGroupSize = nodeGroupSize;
        _progressHandler = (_, _) => AddNodesBatch(isFinal: false);
        _completedHandler = () => AddNodesBatch(isFinal: true);
        TreeBuilder = new DelegateTreeBuilder<ITreeNode>(
            // canExpand = branch/leaf (shows expand/collapse icon); IsExpanded = current expansion state (open/closed).
            canExpand: node =>
                // Type-based check only — never access Children here.
                // JsonLinesRangeTreeNode: used for files > 1,000 lines; always expandable by design as it contains ≥ 1 line.
                // JsonObjectTreeNode/JsonArrayTreeNode: line nodes loaded directly; Children is not accessed
                // to preserve lazy loading (accessing Children triggers full JSON parse immediately).
                node is JsonLinesRangeTreeNode or JsonObjectTreeNode or JsonArrayTreeNode,
            childGetter: node =>
            {
                if (node is JsonLinesRangeTreeNode r)
                {
                    r.EnsureChildrenLoaded();
                }

                return node.Children;
            }
        );
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

    private void StartProgressiveLoading()
    {
        _indexer.ProgressChanged += _progressHandler;
        _indexer.BuildIndexCompleted += _completedHandler;

        if (_indexer.IsIndexingCompleted)
        {
            _completedHandler();
        }
    }

    /// <summary>
    /// Adds range nodes in batches based on current indexing progress.
    /// Called from both <c>ProgressChanged</c> and <c>BuildIndexCompleted</c> handlers.
    /// </summary>
    /// <param name="isFinal">
    /// <c>true</c> when called from <c>BuildIndexCompleted</c> to add the remainder node;
    /// <c>false</c> when called from <c>ProgressChanged</c> for progressive addition.
    /// </param>
    private void AddNodesBatch(bool isFinal)
    {
        var currentRows = _indexer.TotalRows;
        var totalSuperRangeNodes = currentRows / _nodeGroupSize;
        var from = Volatile.Read(ref _addedSuperRangeNodeCount);

        if (from < totalSuperRangeNodes)
        {
            from = Interlocked.Exchange(ref _addedSuperRangeNodeCount, totalSuperRangeNodes);

            for (var g = from; g < totalSuperRangeNodes; g++)
            {
                var startIndex = g * _nodeGroupSize;
                _uiThreadInvoke(() =>
                    AddObject(new JsonLinesRangeTreeNode(_indexer, _reader, startIndex, _nodeGroupSize)));
            }
        }

        if (!isFinal)
        {
            return;
        }

        if (Interlocked.Exchange(ref _finalHandled, 1) != 0)
        {
            return;
        }

        var remainder = currentRows % _nodeGroupSize;
        if (remainder > 0)
        {
            _uiThreadInvoke(() =>
                AddObject(new JsonLinesRangeTreeNode(
                    _indexer, _reader, totalSuperRangeNodes * _nodeGroupSize, remainder)));
        }
    }

    /// <summary>
    /// Builds all nodes at once using the actual <paramref name="totalRows"/> count.
    /// Called when <see cref="IRowIndexer.IsIndexingCompleted"/> is <c>true</c> at creation time.
    /// </summary>
    /// <param name="totalRows">The actual total number of rows from the completed indexer.</param>
    private void BuildInitialNodes(long totalRows)
    {
        if (totalRows <= 0)
        {
            return;
        }

        if (totalRows <= RangePartitionPolicy.RangeSize)
        {
            var (byteOffset, rowOffset) = _indexer.GetCheckPoint(0);
            var lines = _reader.ReadLineBytes(byteOffset, rowOffset, (int)totalRows);
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].IsEmpty)
                {
                    continue;
                }

                AddObject(JsonLinesRangeTreeNode.CreateLineNode(lines[i], i));
            }

            return;
        }

        var totalRangeNodes = (totalRows + _nodeGroupSize - 1) / _nodeGroupSize;
        for (var g = 0L; g < totalRangeNodes; g++)
        {
            var startIndex = g * _nodeGroupSize;
            var count = Math.Min(_nodeGroupSize, totalRows - startIndex);
            AddObject(new JsonLinesRangeTreeNode(_indexer, _reader, startIndex, count));
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _indexer.ProgressChanged -= _progressHandler;
            _indexer.BuildIndexCompleted -= _completedHandler;
            _reader.Dispose();
        }

        base.Dispose(disposing);
    }
}
