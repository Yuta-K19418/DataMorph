namespace DataMorph.Engine.Models;

/// <summary>
/// Describes a runtime value transformation applied to a single output column cell.
/// Sealed subtypes are handled via pattern matching in RecordProcessor.
/// </summary>
public abstract record CellTransformSpec;

/// <summary>Replace every cell value with a fixed constant string.</summary>
public sealed record FillSpec(string Value) : CellTransformSpec;
