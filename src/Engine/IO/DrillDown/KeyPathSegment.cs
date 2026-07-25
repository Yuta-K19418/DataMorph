namespace Refedle.Engine.IO.DrillDown;

/// <summary>
/// One tagged segment of a DrillDown KeyPath: an object property name or an array-element index.
/// Carrying the kind explicitly removes ambiguity between an index marker such as "[0]" and a
/// literal object key that starts with '[' — which would otherwise be misread as an index and
/// cause traversal to silently skip every record.
/// </summary>
/// <param name="Value">The property name (e.g. "orders") or the index label (e.g. "[0]").</param>
/// <param name="Kind">Whether <paramref name="Value"/> addresses an object property or an array element.</param>
public readonly record struct KeyPathSegment(string Value, KeyPathSegmentKind Kind);
