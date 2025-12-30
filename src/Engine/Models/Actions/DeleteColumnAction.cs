namespace DataMorph.Engine.Models.Actions;

/// <summary>
/// Deletes a column from the dataset.
/// </summary>
public sealed record DeleteColumnAction : MorphAction
{
    /// <summary>
    /// The name of the column to delete.
    /// </summary>
    public required string ColumnName { get; init; }

    public override string Description => $"Delete column '{ColumnName}'";
}
