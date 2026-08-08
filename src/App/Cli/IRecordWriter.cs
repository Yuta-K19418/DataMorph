namespace Refedle.App.Cli;

internal interface IRecordWriter : IDisposable, IAsyncDisposable
{
    ValueTask WriteHeaderAsync(CancellationToken ct);
    ValueTask WriteStartRecordAsync(CancellationToken ct);
    void WriteCellData(int outputColumnIndex, CellData cell);
    ValueTask WriteEndRecordAsync(CancellationToken ct);
    ValueTask FlushAsync(CancellationToken ct);
}
