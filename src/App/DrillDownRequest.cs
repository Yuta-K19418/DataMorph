using DataMorph.Engine.Types;

namespace DataMorph.App;

/// <summary>Base type for DrillDown command requests.</summary>
/// <param name="Format">Source file format.</param>
internal abstract record DrillDownRequestBase(DataFormat Format);

/// <summary>Single-node DrillDown: JSON Object format, operates on the selected node only.</summary>
/// <param name="Format">Source file format.</param>
/// <param name="NodeBytes">Raw bytes of the selected node's JSON value.</param>
internal sealed record SingleDrillDownRequest(
    DataFormat Format,
    JsonRawBytes NodeBytes)
    : DrillDownRequestBase(Format);

/// <summary>Full-aggregation DrillDown: JSON Lines / JSON Array format, scans the entire file.</summary>
/// <param name="Format">Source file format.</param>
/// <param name="KeyPath">Ordered path segments from root to the selected node.</param>
internal sealed record FullAggregationDrillDownRequest(
    DataFormat Format,
    IReadOnlyList<string> KeyPath)
    : DrillDownRequestBase(Format);
