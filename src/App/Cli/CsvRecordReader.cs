using DataMorph.Engine;
using DataMorph.Engine.Filtering;
using nietras.SeparatedValues;

namespace DataMorph.App.Cli;

internal struct CsvRecordReader : IRecordReader, IDisposable
{
    private readonly SepReader _reader;
    private readonly int[] _outputToSourceIndexMap;
    private readonly IReadOnlyList<FilterSpec> _filters;

    public CsvRecordReader(SepReader reader, BatchOutputSchema outputSchema)
    {
        _reader = reader;

        var header = _reader.Header;
        var sourceNameToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.ColNames.Count; i++)
        {
            sourceNameToIndex[header.ColNames[i]] = i;
        }

        _outputToSourceIndexMap = new int[outputSchema.Columns.Count];
        for (var i = 0; i < outputSchema.Columns.Count; i++)
        {
            var col = outputSchema.Columns[i];
            _outputToSourceIndexMap[i] = sourceNameToIndex.TryGetValue(col.SourceName, out var idx) ? idx : -1;
        }

        _filters = outputSchema.Filters;
    }

    public ValueTask<bool> MoveNextAsync(CancellationToken ct)
    {
        return _reader.MoveNextAsync(ct);
    }

    public readonly bool EvaluateFilters()
    {
        return FilterEvaluator.EvaluateCsvFilters(_reader.Current, _filters);
    }

    public readonly ReadOnlySpan<char> GetCellSpan(int outputColumnIndex)
    {
        var sourceIndex = _outputToSourceIndexMap[outputColumnIndex];
        if (sourceIndex >= 0 && sourceIndex < _reader.Current.ColCount)
        {
            return _reader.Current[sourceIndex].Span;
        }
        return [];
    }

    public void Dispose()
    {
        _reader.Dispose();
    }
}
