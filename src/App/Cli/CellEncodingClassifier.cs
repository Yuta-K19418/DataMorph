namespace Refedle.App.Cli;

/// <summary>
///  Shared text-to-<see cref="CellEncoding"/> heuristic used by CSV reads and by
///  transformed-column output. Mirrors the historical <c>WriteJsonValue</c>
///  detection order (bool → long → double → text) so CSV→JSON Lines and
///  transformed-column output stay byte-for-byte unchanged.
/// </summary>
internal static class CellEncodingClassifier
{
    /// <summary>
    ///  Classifies plain cell text into the encoding the JSON Lines writer needs.
    ///  Step 2 implements the exact bool/long/double heuristic.
    /// </summary>
    public static CellEncoding Classify(ReadOnlySpan<char> value)
    {
        throw new NotImplementedException();
    }
}
