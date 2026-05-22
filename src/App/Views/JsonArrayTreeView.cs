using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;

namespace DataMorph.App.Views;

/// <summary>
/// <see cref="MorphTreeView"/> subclass for JSON Array files.
/// Creates <see cref="ElementByteCache"/> and populates the tree root directly.
/// For ≤ 1,000 items, element nodes are added directly via <see cref="JsonArrayRangeTreeNode.CreateElementNode"/>.
/// For ≥ 1,001 items, 1,000-item <see cref="JsonArrayRangeTreeNode"/> instances are created instead.
/// </summary>
internal sealed class JsonArrayTreeView : MorphTreeView
{
    private const int RangeSize = 1_000;
    private readonly ElementByteCache _cache;

    private JsonArrayTreeView(ElementByteCache cache, Action onTableModeToggle)
        : base(onTableModeToggle)
    {
        _cache = cache;
    }

    internal static JsonArrayTreeView Create(IRowIndexer indexer, Action onTableModeToggle)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        var cache = new ElementByteCache(indexer);
        var view = new JsonArrayTreeView(cache, onTableModeToggle);
        var totalRows = indexer.TotalRows;

        if (totalRows > int.MaxValue)
        {
            throw new NotSupportedException($"JSON Arrays exceeding {int.MaxValue} elements are not supported.");
        }

        if (totalRows <= RangeSize)
        {
            for (var i = 0; i < totalRows; i++)
            {
                var bytes = cache.GetRow(i);
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
            view.AddObject(new JsonArrayRangeTreeNode(cache, start, (int)count));
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
