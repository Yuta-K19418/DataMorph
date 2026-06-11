using Terminal.Gui.Views;

namespace DataMorph.App.Views.JsonRangeTreeNodes;

/// <summary>
/// Abstract base class for range-based tree nodes (<see cref="JsonLinesRangeTreeNode"/>,
/// <see cref="JsonArrayRangeTreeNode"/>). Provides common file-size-based calculation logic
/// and a lazy-loading template method for children.
/// </summary>
internal abstract class RangeTreeNodeBase : TreeNode
{
    protected const int RangeSize = 1_000;

    /// <summary>
    /// Calculates the node group size based on file size.
    /// Returns <see cref="RangeSize"/> for estimated rows ≤ 1,000,000,
    /// or a super-range size (multiple of <see cref="RangeSize"/>) for larger files.
    /// </summary>
    protected static long GetNodeGroupSize(long fileSize)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Whether children have been loaded at least once.
    /// Used by <see cref="DelegateTreeBuilder{ITreeNode}"/> to determine expand-arrow visibility
    /// without touching <see cref="Children"/> (which would trigger premature loading).
    /// </summary>
    internal bool IsChildrenLoaded { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// No lazy loading — <see cref="DelegateTreeBuilder{ITreeNode}"/> controls load timing
    /// via <see cref="EnsureChildrenLoaded"/> to prevent <c>TreeView.AddObject()</c> from
    /// triggering eager child retrieval.
    /// </remarks>
    public override IList<ITreeNode> Children => base.Children;

    /// <summary>
    /// Loads children from the reader if not already loaded.
    /// Called by the <see cref="DelegateTreeBuilder{ITreeNode}"/> child getter on expansion.
    /// </summary>
    internal void EnsureChildrenLoaded()
    {
        if (IsChildrenLoaded)
        {
            return;
        }

        LoadChildren();
        IsChildrenLoaded = true;
    }

    /// <summary>
    /// Loads children for this range node. Implemented by derived classes.
    /// When <c>_count &gt; RangeSize</c>, creates nested range nodes;
    /// otherwise, creates individual line/element nodes.
    /// </summary>
    protected abstract void LoadChildren();
}
