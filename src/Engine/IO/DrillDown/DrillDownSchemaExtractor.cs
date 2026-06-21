using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.IO.DrillDown;

/// <summary>
/// Parses a selected node's raw bytes in memory and returns the inferred schema
/// and an ordered list of child object bytes.
/// </summary>
public static class DrillDownSchemaExtractor
{
    /// <summary>
    /// Parses <paramref name="nodeBytes"/> as a JSON array whose direct children must all be
    /// JSON Objects. Infers a <see cref="TableSchema"/> (union of top-level keys) and returns
    /// the ordered child value bytes.
    /// Returns <c>Failure</c> when children include non-Objects, the array is empty, or the
    /// JSON is malformed.
    /// </summary>
    public static Result<(TableSchema schema, IReadOnlyList<ReadOnlyMemory<byte>> childValueBytes)>
        ExtractFromNode(ReadOnlyMemory<byte> nodeBytes, DataFormat format) =>
        throw new NotImplementedException();
}
