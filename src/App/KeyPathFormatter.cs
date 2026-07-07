using DataMorph.Engine.IO.DrillDown;

namespace DataMorph.App;

/// <summary>
/// Formats a KeyPath into the breadcrumb display string (e.g. <c>"root → data → orders[*]"</c>).
/// </summary>
internal static class KeyPathFormatter
{
    /// <summary>
    /// Renders <paramref name="path"/> as a single breadcrumb line, collapsing every
    /// <see cref="KeyPathSegmentKind.Index"/> segment to <c>"[*]"</c> when
    /// <paramref name="collapseIndices"/> is <c>true</c> (Full Aggregation DrillDown semantics),
    /// or keeping the concrete index otherwise.
    /// </summary>
    /// <param name="path">The ordered path segments from root to the selected node.</param>
    /// <param name="collapseIndices">
    /// When <c>true</c>, every <see cref="KeyPathSegmentKind.Index"/> segment renders as <c>"[*]"</c>.
    /// </param>
    /// <returns>The formatted breadcrumb text. An empty path yields <c>"root"</c>.</returns>
    internal static string Format(IReadOnlyList<KeyPathSegment> path, bool collapseIndices)
    {
        throw new NotImplementedException();
    }
}
