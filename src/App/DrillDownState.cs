using DataMorph.Engine.IO.DrillDown;
using DataMorph.Engine.Models;

namespace DataMorph.App;

/// <summary>
/// Holds the in-memory state produced by the DrillDown command.
/// </summary>
internal sealed record DrillDownState(
    IReadOnlyList<FocusedTableRow> Rows,
    TableSchema Schema);
