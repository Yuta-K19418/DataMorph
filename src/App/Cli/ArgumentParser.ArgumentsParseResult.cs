namespace DataMorph.App.Cli;

internal static partial class ArgumentParser
{
    private sealed class ArgumentsParseResult
    {
        public string? InputFile { get; set; }
        public string? RecipeFile { get; set; }
        public string? OutputFile { get; set; }
    }
}
