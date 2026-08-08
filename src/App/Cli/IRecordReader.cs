namespace Refedle.App.Cli;

internal interface IRecordReader : IDisposable
{
    ValueTask<bool> MoveNextAsync(CancellationToken ct);
    bool EvaluateFilters();
    CellData GetCellData(int outputColumnIndex);
}
