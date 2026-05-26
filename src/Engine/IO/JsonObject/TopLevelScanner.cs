namespace DataMorph.Engine.IO.JsonObject;

internal sealed class TopLevelScanner
{
#pragma warning disable CA1823
    private const int InitialBufferSize = 1024 * 1024;      // 1 MB
    private const int MaxBufferSize = 16 * 1024 * 1024; // 16 MB
#pragma warning restore CA1823

    internal static IReadOnlyList<(string key, ReadOnlyMemory<byte> value)> Scan(
        string filePath,
        CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
