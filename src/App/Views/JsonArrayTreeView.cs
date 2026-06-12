using DataMorph.App.Views.JsonRangeTreeNodes;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// <see cref="MorphTreeView"/> subclass for JSON Array files.
/// Creates <see cref="ElementReader"/> and populates the tree root.
/// Supports lazy loading with progressive node addition for large files.
/// </summary>
internal sealed class JsonArrayTreeView : MorphTreeView
{
    private readonly IRowIndexer _indexer;
    private readonly ElementReader _reader;
    private readonly Action<Action> _uiThreadInvoke;
    private readonly long _nodeGroupSize;
    private readonly Action<long, long> _progressHandler;
    private readonly Action _completedHandler;
#pragma warning disable CA1823, CS0169, IDE0044 // Interlocked counter fields; assigned via Interlocked.Exchange in event handlers
    private long _addedSuperRangeNodeCount;
    private int _finalHandled;
#pragma warning restore CA1823, CS0169, IDE0044

    private JsonArrayTreeView(
        IRowIndexer indexer,
        ElementReader reader,
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
                // JsonArrayRangeTreeNode: used for arrays > 1,000 elements; always expandable by design as it contains ≥ 1 element.
                // JsonObjectTreeNode/JsonArrayTreeNode: accessing Children would trigger an
                // immediate JSON parse of the element, bypassing lazy loading.
                node is JsonArrayRangeTreeNode or JsonObjectTreeNode or JsonArrayTreeNode,
            childGetter: node =>
            {
                if (node is JsonArrayRangeTreeNode r)
                {
                    r.EnsureChildrenLoaded();
                }

                return node.Children;
            }
        );
    }

    /// <summary>
    /// Factory method that creates a <see cref="JsonArrayTreeView"/> for the given JSON Array file.
    /// </summary>
    /// <param name="indexer">The row indexer for the JSON Array file.</param>
    /// <param name="onTableModeToggle">Callback invoked when the user presses 't'.</param>
    /// <param name="uiThreadInvoke">Marshals actions to the UI thread for thread-safe <c>AddObject</c> calls.</param>
    /// <returns>A new <see cref="JsonArrayTreeView"/> instance populated with element or range nodes.</returns>
    internal static JsonArrayTreeView Create(
        IRowIndexer indexer,
        Action onTableModeToggle,
        Action<Action> uiThreadInvoke)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        ArgumentNullException.ThrowIfNull(uiThreadInvoke);

        var nodeGroupSize = RangePartitionPolicy.GetNodeGroupSize(indexer.FileSize);
        var view = new JsonArrayTreeView(
            indexer, new ElementReader(indexer.FilePath), onTableModeToggle, uiThreadInvoke, nodeGroupSize);

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
                    AddObject(new JsonArrayRangeTreeNode(_indexer, _reader, startIndex, _nodeGroupSize)));
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
                AddObject(new JsonArrayRangeTreeNode(
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
            var elements = _reader.ReadElementBytes(byteOffset, rowOffset, (int)totalRows);
            for (var i = 0; i < elements.Count; i++)
            {
                if (elements[i].IsEmpty)
                {
                    continue;
                }

                AddObject(JsonArrayRangeTreeNode.CreateElementNode(elements[i], i));
            }

            return;
        }

        var totalRangeNodes = (totalRows + _nodeGroupSize - 1) / _nodeGroupSize;
        for (var g = 0L; g < totalRangeNodes; g++)
        {
            var startIndex = g * _nodeGroupSize;
            var count = Math.Min(_nodeGroupSize, totalRows - startIndex);
            AddObject(new JsonArrayRangeTreeNode(_indexer, _reader, startIndex, count));
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
