using System.Text;
using DataMorph.Engine;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;

namespace DataMorph.App.Cli;

internal struct JsonLinesRecordReader : IRecordReader, IDisposable
{
    private readonly RowIndexer _rowIndexer;
    private readonly RowReader _rowReader;
    private readonly Memory<byte>[] _columnNameUtf8Bytes;
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _filterIndexToNameBytes;
    private readonly IReadOnlyList<Engine.Filtering.FilterSpec> _filters;

    private long _batchStart;
    private IReadOnlyList<ReadOnlyMemory<byte>> _currentBatch;
    private int _batchIndex;
    private ReadOnlyMemory<byte> _currentLineBytes;

    public JsonLinesRecordReader(RowIndexer rowIndexer, RowReader rowReader, TableSchema inputSchema, BatchOutputSchema outputSchema)
    {
        _rowIndexer = rowIndexer;
        _rowReader = rowReader;

        _columnNameUtf8Bytes = [.. outputSchema.Columns
            .Select(c => Encoding.UTF8.GetBytes(c.SourceName).AsMemory())];

        _filterIndexToNameBytes = inputSchema.Columns
            .ToDictionary(c => c.ColumnIndex, c => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(c.Name));

        _filters = outputSchema.Filters;

        _batchStart = 0;
        _currentBatch = [];
        _batchIndex = -1;
        _currentLineBytes = default;
    }

    public ValueTask<bool> MoveNextAsync(CancellationToken ct)
    {
        while (true)
        {
            _batchIndex++;
            if (_batchIndex < _currentBatch.Count)
            {
                _currentLineBytes = _currentBatch[_batchIndex];
                if (_currentLineBytes.IsEmpty || FilterEvaluator.IsWhiteSpace(_currentLineBytes.Span))
                {
                    continue;
                }
                return new ValueTask<bool>(true);
            }

            if (_batchStart >= _rowIndexer.TotalRows)
            {
                return new ValueTask<bool>(false);
            }

            ct.ThrowIfCancellationRequested();

            var (byteOffset, rowOffset) = _rowIndexer.GetCheckPoint(_batchStart);
            var linesToRead = (int)Math.Min(1000, _rowIndexer.TotalRows - _batchStart);

            _currentBatch = _rowReader.ReadLineBytes(byteOffset, rowOffset, linesToRead);
            _batchStart += linesToRead;
            _batchIndex = -1;
        }
    }

    public readonly bool EvaluateFilters()
    {
        return FilterEvaluator.EvaluateJsonFilters(_currentLineBytes, _filters, _filterIndexToNameBytes);
    }

    public readonly ReadOnlySpan<char> GetCellSpan(int outputColumnIndex)
    {
        var columnNameSpan = _columnNameUtf8Bytes[outputColumnIndex].Span;
        var value = CellExtractor.ExtractCell(_currentLineBytes.Span, columnNameSpan);
        return value.AsSpan();
    }

    public void Dispose()
    {
        _rowReader?.Dispose();
    }
}
