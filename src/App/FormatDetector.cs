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
            _ => Results.Failure<DataFormat>($"Unsupported file format: {rawExtension}"),
        };
    }
}
