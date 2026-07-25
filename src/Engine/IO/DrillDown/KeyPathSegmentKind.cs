namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// Whether a <see cref="KeyPathSegment"/> addresses a JSON object property or a JSON array element.
/// </summary>
public enum KeyPathSegmentKind
{
    /// <summary>The segment is an object property name (e.g. "orders").</summary>
    Key,

    /// <summary>The segment is an array-element index label (e.g. "[0]").</summary>
    Index,
}
