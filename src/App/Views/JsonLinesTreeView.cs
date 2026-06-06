using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// A <see cref="MorphTreeView"/> that displays JSON Lines data as a hierarchical tree.
/// Creates TreeNode instances from raw JSON bytes provided by the Engine layer.
/// For files with ≤ 1,000 lines, line nodes are added directly.
/// For larger files, lines are grouped into <see cref="JsonLinesRangeTreeNode"/> ranges of 1,000.
/// </summary>
internal sealed class JsonLinesTreeView : MorphTreeView
{
    private const int RangeSize = 1_000;
    private readonly RowByteCache _cache;

    private JsonLinesTreeView(RowByteCache cache, Action onTableModeToggle)
        : base(onTableModeToggle)
    {
        _cache = cache;
        TreeBuilder = new DelegateTreeBuilder<ITreeNode>(
            // canExpand = branch/leaf (shows expand/collapse icon); IsExpanded = current expansion state (open/closed).
            canExpand: node =>
                // JsonLinesRangeTreeNode is always a branch by design; other nodes are branches only when they have children.
                node is JsonLinesRangeTreeNode || node.Children.Count > 0,
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
    /// <returns>A new <see cref="JsonLinesTreeView"/> instance populated with line or range nodes.</returns>
    internal static JsonLinesTreeView Create(IRowIndexer indexer, Action onTableModeToggle)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        var totalRows = indexer.TotalRows;

        if (totalRows > int.MaxValue)
        {
            throw new NotSupportedException(
                $"JSON Lines files exceeding {int.MaxValue} lines are not supported."
            );
        }

        var rowCount = (int)totalRows;
        var cache = new RowByteCache(indexer);
        var view = new JsonLinesTreeView(cache, onTableModeToggle);

        if (rowCount <= RangeSize)
        {
            for (var i = 0; i < rowCount; i++)
            {
                var bytes = cache.GetRow(i);
                if (bytes.IsEmpty)
                {
                    continue;
                }

                view.AddObject(JsonLinesRangeTreeNode.CreateLineNode(bytes, i));
            }

            return view;
        }

        var start = 0;
        while (start < rowCount)
        {
            var count = Math.Min(RangeSize, rowCount - start);
            view.AddObject(new JsonLinesRangeTreeNode(cache, start, count));
            start += RangeSize;
        }

        return view;
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
