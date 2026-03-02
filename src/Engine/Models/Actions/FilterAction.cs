namespace DataMorph.Engine.Models.Actions;

/// <summary>
/// A row-level filter action that retains only source rows satisfying a column value condition.
/// Multiple <see cref="FilterAction"/>s in the action stack are applied with AND semantics.
/// </summary>
public sealed record FilterAction : MorphAction
{
    /// <summary>The name of the column to filter on.</summary>
    public required string ColumnName { get; init; }

    /// <summary>The comparison operator.</summary>
    public required FilterOperator Operator { get; init; }

    /// <summary>The value to compare against (raw string).</summary>
    public required string Value { get; init; }

    /// <inheritdoc/>
    public override string Description => $"Filter '{ColumnName}' {Operator} '{Value}'";
}
