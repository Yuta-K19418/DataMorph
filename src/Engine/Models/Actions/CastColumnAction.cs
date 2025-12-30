using DataMorph.Engine.Types;

namespace DataMorph.Engine.Models.Actions;

/// <summary>
/// Casts a column to a different data type.
/// </summary>
public sealed record CastColumnAction : MorphAction
{
    /// <summary>
    /// The name of the column to cast.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// The target type for the cast operation.
    /// </summary>
    public required ColumnType TargetType { get; init; }

    public override string Description => $"Cast column '{ColumnName}' to {TargetType}";
}
