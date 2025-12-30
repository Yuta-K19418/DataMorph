namespace DataMorph.Engine.Models.Actions;

/// <summary>
/// Renames a column in the dataset.
/// </summary>
public sealed record RenameColumnAction : MorphAction
{
    /// <summary>
    /// The current name of the column to rename.
    /// </summary>
    public required string OldName { get; init; }

    /// <summary>
    /// The new name for the column.
    /// </summary>
    public required string NewName { get; init; }

    public override string Description => $"Rename column '{OldName}' to '{NewName}'";
}
