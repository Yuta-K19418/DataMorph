namespace DataMorph.App;

/// <summary>
/// Holds optional startup arguments for TUI mode.
/// </summary>
internal sealed record TuiStartupOptions(string? InputFile = null, string? RecipeFile = null)
{
    public bool HasAny => InputFile is not null || RecipeFile is not null;
}
