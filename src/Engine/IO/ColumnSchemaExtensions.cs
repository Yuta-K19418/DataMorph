using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.IO;

/// <summary>
/// Extension methods for ColumnSchema to handle type updates with early-exit optimization.
/// </summary>
public static class ColumnSchemaExtensions
{
    /// <summary>
    /// Updates the column type if the observed type differs from current.
    /// </summary>
    /// <param name="schema">The column schema to update.</param>
    /// <param name="observedType">The newly observed type.</param>
    /// <remarks>
    /// If current type equals observed type, returns immediately (no-op).
    /// Otherwise, calls ColumnTypeResolver.Resolve() and updates the schema.
    /// </remarks>
    public static void UpdateColumnType(this ColumnSchema schema, ColumnType observedType)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.Type == observedType)
        {
            return; // Early exit - no change needed
        }

        schema.Type = ColumnTypeResolver.Resolve(schema.Type, observedType);
    }

    /// <summary>
    /// Marks the column as nullable if not already.
    /// </summary>
    public static void MarkNullable(this ColumnSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        schema.IsNullable = true;
    }
}
