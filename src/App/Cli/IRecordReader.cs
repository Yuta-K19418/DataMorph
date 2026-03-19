namespace DataMorph.App.Cli;

internal interface IRecordReader : IDisposable
{
    ValueTask<bool> MoveNextAsync(CancellationToken ct);
    bool EvaluateFilters();
    ReadOnlySpan<char> GetCellSpan(int outputColumnIndex);
}
