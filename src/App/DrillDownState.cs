using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.App;

/// <summary>
/// Holds the in-memory state produced by the DrillDown command.
/// </summary>
internal sealed record DrillDownState(
    IReadOnlyList<JsonRawBytes> ChildValueBytes,
    TableSchema Schema,
    DataFormat Format,
    long? RecordPosition);
