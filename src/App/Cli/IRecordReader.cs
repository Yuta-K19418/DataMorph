namespace DataMorph.App.Cli;

internal interface IRecordReader
{
    ValueTask<bool> MoveNextAsync(CancellationToken ct);
    bool EvaluateFilters();
    ReadOnlySpan<char> GetCellSpan(int outputColumnIndex);
}
