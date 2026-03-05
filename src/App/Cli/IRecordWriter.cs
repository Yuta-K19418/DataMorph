namespace DataMorph.App.Cli;

internal interface IRecordWriter : IDisposable, IAsyncDisposable
{
    ValueTask WriteHeaderAsync(CancellationToken ct);
    ValueTask WriteStartRecordAsync(CancellationToken ct);
    void WriteCellSpan(int outputColumnIndex, ReadOnlySpan<char> value);
    ValueTask WriteEndRecordAsync(CancellationToken ct);
    ValueTask FlushAsync(CancellationToken ct);
}
