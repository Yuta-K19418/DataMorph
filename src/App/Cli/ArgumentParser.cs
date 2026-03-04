using DataMorph.Engine;

namespace DataMorph.App.Cli;

/// <summary>
/// Parses command-line arguments into an <see cref="Arguments"/> record.
/// Accepts named flags in the form <c>--key value</c>.
/// Required flags: <c>--cli</c>, <c>--input</c>, <c>--recipe</c>, <c>--output</c>.
/// Unknown flags are rejected.
/// </summary>
internal static partial class ArgumentParser
{
    private const string InputFlag = "--input";
    private const string RecipeFlag = "--recipe";
    private const string OutputFlag = "--output";
    private const string CliFlag = "--cli";

    /// <summary>
    /// Parses the given command-line argument array into an <see cref="Arguments"/> record.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing the parsed arguments,
    /// or a failure with a human-readable error message.
    /// </returns>
    public static Result<Arguments> Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return Results.Failure<Arguments>("No arguments provided");
        }

        var result = new ArgumentsParseResult();
        var i = 0;

        while (i < args.Count)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return Results.Failure<Arguments>($"Invalid flag: '{args[i]}'");
            }

            if (args[i].Equals(CliFlag, StringComparison.Ordinal))
            {
                i += 1;
                continue;
            }

            if (args[i].Equals(InputFlag, StringComparison.Ordinal))
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return Results.Failure<Arguments>($"Missing value for {InputFlag}");
                }

                result.InputFile = args[i + 1];
                i += 2;
                continue;
            }

            if (args[i].Equals(RecipeFlag, StringComparison.Ordinal))
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return Results.Failure<Arguments>($"Missing value for {RecipeFlag}");
                }

                result.RecipeFile = args[i + 1];
                i += 2;
                continue;
            }

            if (args[i].Equals(OutputFlag, StringComparison.Ordinal))
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return Results.Failure<Arguments>($"Missing value for {OutputFlag}");
                }

                result.OutputFile = args[i + 1];
                i += 2;
                continue;
            }

            return Results.Failure<Arguments>($"Unknown flag: {args[i]}");
        }

        if (string.IsNullOrWhiteSpace(result.InputFile))
        {
            return Results.Failure<Arguments>($"Missing required flag: {InputFlag}");
        }

        if (string.IsNullOrWhiteSpace(result.RecipeFile))
        {
            return Results.Failure<Arguments>($"Missing required flag: {RecipeFlag}");
        }

        if (string.IsNullOrWhiteSpace(result.OutputFile))
        {
            return Results.Failure<Arguments>($"Missing required flag: {OutputFlag}");
        }

        return Results.Success(new Arguments
        {
            InputFile = result.InputFile,
            RecipeFile = result.RecipeFile,
            OutputFile = result.OutputFile,
        });
    }
}
