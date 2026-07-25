using Refedle.Engine.IO.DrillDown;
using Refedle.Engine.Models;

namespace Refedle.App;

/// <summary>
/// Holds the in-memory state produced by the DrillDown command.
/// </summary>
internal sealed record DrillDownState(
    IReadOnlyList<FocusedTableRow> Rows,
    TableSchema Schema,
    ViewMode PreviousMode);
