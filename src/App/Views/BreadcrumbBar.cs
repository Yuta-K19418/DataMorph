using DataMorph.Engine.IO.DrillDown;
using Terminal.Gui.ViewBase;

namespace DataMorph.App.Views;

/// <summary>
/// A single-row bar directly below the <c>MenuBar</c> that renders the current JSON hierarchy
/// location as a breadcrumb. <see cref="SetPath"/> updates the displayed text and the clickable
/// segment ranges; clicking a segment raises <see cref="SegmentActivated"/>.
/// </summary>
internal sealed class BreadcrumbBar : View
{
    /// <summary>
    /// Raised with the 0-based index of the segment the user activated (clicked or otherwise
    /// selected). Subscribers move the tree selection to the corresponding ancestor node.
    /// Invoked from the mouse click handler, which is implemented in Step 2.
    /// </summary>
#pragma warning disable CS0067
    internal event Action<int>? SegmentActivated;
#pragma warning restore CS0067

    internal BreadcrumbBar()
    {
        X = 0;
        Y = 1; // Directly below the MenuBar (row 0)
        Width = Dim.Fill();
        Height = 1;
    }

    /// <summary>
    /// Renders <paramref name="path"/> as the breadcrumb text, collapsing array indices to
    /// <c>"[*]"</c> when <paramref name="collapseIndices"/> is <c>true</c>, and records each
    /// segment's column range so a later click can be mapped back to a segment index.
    /// </summary>
    /// <param name="path">The ordered path segments from root to the current location.</param>
    /// <param name="collapseIndices">
    /// When <c>true</c>, array-element indices render as <c>"[*]"</c> (Full Aggregation DrillDown).
    /// </param>
    internal void SetPath(IReadOnlyList<KeyPathSegment> path, bool collapseIndices)
    {
        throw new NotImplementedException();
    }
}
