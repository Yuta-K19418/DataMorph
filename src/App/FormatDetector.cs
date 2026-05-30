using DataMorph.Engine;
using DataMorph.Engine.Types;

namespace DataMorph.App;

/// <summary>
/// Detects the data format of a file based on its extension.
/// </summary>
internal static class FormatDetector
{
    /// <summary>
    /// Detects the format of the file at the specified path.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <returns>A <see cref="Result{DataFormat}"/> indicating the detected format or failure.</returns>
    public static Result<DataFormat> Detect(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return Results.Failure<DataFormat>("File path cannot be empty");
        }

        if (!File.Exists(filePath))
        {
            return Results.Failure<DataFormat>("File does not exist");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            return Results.Failure<DataFormat>("File is empty");
        }

        // Use ToUpperInvariant for normalization as recommended by CA1308 to avoid
        // potential round-tripping issues in some locales (e.g., Turkish 'I').
        var rawExtension = Path.GetExtension(filePath);
        var extension = rawExtension.ToUpperInvariant();

        return extension switch
        {
            ".CSV" => Results.Success(DataFormat.Csv),
            ".JSONL" => Results.Success(DataFormat.JsonLines),
            ".JSON" => DetectJsonFormat(filePath),
            _ => Results.Failure<DataFormat>($"Unsupported file format: {rawExtension}"),
        };
    }

    /// <summary>
    /// Distinguishes JSON Object from JSON Array by peeking at the first non-whitespace byte.
    /// </summary>
    /// <param name="filePath">Path to the .json file.</param>
    /// <returns>
    /// <see cref="DataFormat.JsonObject"/> if the root token is <c>{</c>,
    /// <see cref="DataFormat.JsonArray"/> if the root token is <c>[</c>,
    /// or a failure result for any other root token or whitespace-only content.
    /// </returns>
    private static Result<DataFormat> DetectJsonFormat(string filePath)
    {
        // TODO: Implement in Step 2 — detect object vs array by reading first token
        return Results.Success(DataFormat.JsonArray);
    }
}
