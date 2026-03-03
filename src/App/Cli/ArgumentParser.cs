using DataMorph.Engine;

namespace DataMorph.App.Cli;

/// <summary>
/// Parses command-line arguments into an <see cref="Arguments"/> record.
/// Accepts named flags in the form <c>--key value</c>.
/// Required flags: <c>--cli</c>, <c>--input</c>, <c>--recipe</c>, <c>--output</c>.
/// Unknown flags are rejected.
/// </summary>
internal static class ArgumentParser
{
    /// <summary>
    /// Parses the given command-line argument array into an <see cref="Arguments"/> record.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the parsed arguments,
    /// or a failure with a human-readable error message.
    /// </returns>
    public static Result<Arguments> Parse(string[] args)
    {
        throw new NotImplementedException();
    }
}
