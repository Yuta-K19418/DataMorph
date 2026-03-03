namespace DataMorph.App.Cli;

/// <summary>
/// Holds validated CLI argument values parsed from the command line.
/// </summary>
internal sealed record Arguments
{
    /// <summary>Gets the path to the input file.</summary>
    public required string InputFile { get; init; }

    /// <summary>Gets the path to the recipe YAML file.</summary>
    public required string RecipeFile { get; init; }

    /// <summary>Gets the path to the output file.</summary>
    public required string OutputFile { get; init; }
}
