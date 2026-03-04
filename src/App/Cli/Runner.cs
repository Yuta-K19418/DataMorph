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
    /// <returns>Exit code: <c>0</c> on success, <c>1</c> on any failure.</returns>
    public static async ValueTask<int> RunAsync(Arguments args, IAppLogger logger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        try
        {
            // Load recipe
            var recipeResult = await new RecipeManager().LoadAsync(args.RecipeFile, ct).ConfigureAwait(false);
            if (recipeResult.IsFailure)
            {
                await logger.WriteErrorAsync($"Error loading recipe: {recipeResult.Error}");
                return 1;
            }

            var recipe = recipeResult.Value;

            // Detect formats
            var inputFormat = DetectFileFormat(args.InputFile);
            if (inputFormat == DataFormat.JsonArray || inputFormat == DataFormat.JsonObject)
            {
                await logger.WriteErrorAsync($"Unsupported input format: {inputFormat}");
                return 1;
            }

            var outputFormat = DetectFileFormat(args.OutputFile);
            if (outputFormat == DataFormat.JsonArray || outputFormat == DataFormat.JsonObject)
            {
                await logger.WriteErrorAsync($"Unsupported output format: {outputFormat}");
                return 1;
            }

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
            return 1;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            await logger.WriteErrorAsync($"Error: {ex.Message}");
            return 1;
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
            ".JSON" => DataFormat.JsonArray,
            _ => DataFormat.JsonLines,
        };
    }

}
