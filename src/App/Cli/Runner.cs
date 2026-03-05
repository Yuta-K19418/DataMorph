using DataMorph.App.Schema.Csv;
using DataMorph.Engine;
using DataMorph.Engine.Models;
using DataMorph.Engine.Recipes;
using DataMorph.Engine.Types;

namespace DataMorph.App.Cli;

/// <summary>
/// Orchestrates the CLI headless batch processing pipeline:
/// recipe load → schema detection → output schema build → transform → write.
/// Supports CSV and JSON Lines for both input and output (cross-format conversion included).
/// </summary>
internal static class Runner
{
    /// <summary>
    /// Runs the CLI headless batch processing pipeline.
    /// </summary>
    /// <param name="args">The validated CLI arguments.</param>
    /// <param name="logger">The app logger for logging messages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <see cref="ExitCode.Success"/> on success, <see cref="ExitCode.Failure"/> on any failure.</returns>
    public static async ValueTask<ExitCode> RunAsync(Arguments args, IAppLogger logger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            // Load recipe
            var recipeResult = await new RecipeManager().LoadAsync(args.RecipeFile, ct).ConfigureAwait(false);
            if (recipeResult.IsFailure)
            {
                await logger.WriteErrorAsync($"Error loading recipe: {recipeResult.Error}");
                return ExitCode.Failure;
            }

            var recipe = recipeResult.Value;

            // Detect formats (throws NotSupportedException if invalid)
            var inputFormat = DetectFileFormat(args.InputFile);
            var outputFormat = DetectFileFormat(args.OutputFile);

            // Scan schema
            var inputSchema = await ScanInputSchemaAsync(args.InputFile, inputFormat).ConfigureAwait(false);

            // Build output schema
            var outputSchema = ActionApplier.BuildOutputSchema(inputSchema, recipe.Actions);

            // Dispatch to generated static monomorphization logic
            return await Generated.FormatDispatcher.DispatchAsync(
                inputFormat, outputFormat, args, inputSchema, outputSchema, logger, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await logger.WriteErrorAsync("Operation cancelled");
            return ExitCode.Failure;
        }
        catch (NotSupportedException ex)
        {
            await logger.WriteErrorAsync(ex.Message);
            return ExitCode.Failure;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            await logger.WriteErrorAsync($"Error: {ex.Message}");
            return ExitCode.Failure;
        }
    }

    private static async ValueTask<TableSchema> ScanInputSchemaAsync(string inputFile, DataFormat inputFormat)
    {
        if (inputFormat == DataFormat.Csv)
        {
            return await new IncrementalSchemaScanner(inputFile).InitialScanAsync().ConfigureAwait(false);
        }

        var jsonLinesScanner = new DataMorph.App.Schema.JsonLines.IncrementalSchemaScanner(inputFile);
        return await jsonLinesScanner.InitialScanAsync().ConfigureAwait(false);
    }

    private static DataFormat DetectFileFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToUpperInvariant();
        return extension switch
        {
            ".CSV" => DataFormat.Csv,
            ".JSONL" => DataFormat.JsonLines,
            // .json is a JSON array/object format, not JSON Lines — unsupported
            ".JSON" => throw new NotSupportedException($"Unsupported format: {extension} (Standard JSON format is not supported for batch processing. Use .jsonl for JSON Lines.)"),
            _ => throw new NotSupportedException($"Unsupported file extension: {extension}"),
        };
    }

}
