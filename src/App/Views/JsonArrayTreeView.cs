using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// <see cref="MorphTreeView"/> subclass for JSON Array files.
/// Creates <see cref="ElementReader"/> and populates the tree root directly.
/// For ≤ 1,000 items, element nodes are added directly via <see cref="JsonArrayRangeTreeNode.CreateElementNode"/>.
/// For ≥ 1,001 items, 1,000-item <see cref="JsonArrayRangeTreeNode"/> instances are created instead.
/// </summary>
internal sealed class JsonArrayTreeView : MorphTreeView
{
    private const int RangeSize = 1_000;
    private readonly ElementReader _reader;

    private JsonArrayTreeView(ElementReader reader, Action onTableModeToggle)
        : base(onTableModeToggle)
    {
        _reader = reader;
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

    internal static JsonArrayTreeView Create(IRowIndexer indexer, Action onTableModeToggle)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        var reader = new ElementReader(indexer.FilePath);
        var view = new JsonArrayTreeView(reader, onTableModeToggle);
        var totalRows = indexer.TotalRows;

        if (totalRows > int.MaxValue)
        {
            throw new NotSupportedException($"JSON Arrays exceeding {int.MaxValue} elements are not supported.");
        }

        if (totalRows <= RangeSize)
        {
            var (byteOffset, rowOffset) = indexer.GetCheckPoint(0);
            var allBytes = reader.ReadElementBytes(byteOffset, rowOffset, (int)totalRows);

            for (var i = 0; i < allBytes.Count; i++)
            {
                var bytes = allBytes[i];
                if (bytes.IsEmpty)
                {
                    continue;
                }

                view.AddObject(JsonArrayRangeTreeNode.CreateElementNode(bytes, i));
            }

            return view;
        }

        var start = 0;
        while (start < totalRows)
        {
            var count = Math.Min(RangeSize, totalRows - start);
            view.AddObject(new JsonArrayRangeTreeNode(indexer, reader, start, (int)count));
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
