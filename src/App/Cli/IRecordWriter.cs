namespace DataMorph.App.Cli;

internal interface IRecordWriter
{
    ValueTask WriteHeaderAsync(CancellationToken ct);
    ValueTask WriteStartRecordAsync(CancellationToken ct);
    void WriteCellSpan(int outputColumnIndex, ReadOnlySpan<char> value);
    ValueTask WriteEndRecordAsync(CancellationToken ct);
    ValueTask FlushAsync(CancellationToken ct);
}
