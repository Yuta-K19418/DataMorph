using DataMorph.App.Views.JsonTreeNodes;
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
    private readonly RowReader _reader;

    private JsonLinesTreeView(RowReader reader, Action onTableModeToggle)
        : base(onTableModeToggle)
    {
        _reader = reader;
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
        var reader = new RowReader(indexer.FilePath);
        var view = new JsonLinesTreeView(reader, onTableModeToggle);

        if (rowCount <= RangeSize)
        {
            var (byteOffset, rowOffset) = indexer.GetCheckPoint(0);
            var allBytes = reader.ReadLineBytes(byteOffset, rowOffset, rowCount);
            for (var i = 0; i < allBytes.Count; i++)
            {
                var bytes = allBytes[i];
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
            view.AddObject(new JsonLinesRangeTreeNode(indexer, reader, start, count));
            start += RangeSize;
        }

        return view;
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
