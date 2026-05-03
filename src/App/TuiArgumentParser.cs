using DataMorph.Engine;

namespace DataMorph.App;

/// <summary>
/// Parses TUI-specific command-line arguments.
/// Accepts optional flags: <c>--file</c> and <c>--recipe</c>.
/// Unknown flags are rejected.
/// </summary>
#pragma warning disable CA1823
internal static class TuiArgumentParser
{
    private const string FileFlag = "--file";
    private const string RecipeFlag = "--recipe";
#pragma warning restore CA1823

    /// <summary>
    /// Parses the given command-line argument array into a <see cref="TuiStartupOptions"/> record.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the parsed arguments,
    /// or a failure with a human-readable error message.
    /// </returns>
    internal static Result<TuiStartupOptions> Parse(IReadOnlyList<string> args)
    {
        throw new NotImplementedException();
    }
}
