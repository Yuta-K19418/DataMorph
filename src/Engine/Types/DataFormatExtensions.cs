namespace DataMorph.Engine.Types;

/// <summary>
/// Provides extension methods for the <see cref="DataFormat"/> enumeration.
/// </summary>
public static class DataFormatExtensions
{
    /// <summary>
    /// Gets the human-readable display name for the specified data format.
    /// </summary>
    /// <param name="source">The data format to get the display name for.</param>
    /// <returns>A string representing the display name of the data format.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the specified <paramref name="source"/> is not a supported data format.
    /// </exception>
    public static string GetDisplayName(this DataFormat source) =>
        source switch
        {
            DataFormat.Csv => "CSV",
            DataFormat.JsonLines => "JSON Lines",
            DataFormat.JsonArray => "JSON Array",
            DataFormat.JsonObject => "JSON Object",
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source,
                $"The data format '{source}' is not supported."
            ),
        };
}
