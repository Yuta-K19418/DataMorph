using System.Text;
using DataMorph.Engine;
using DataMorph.Engine.Filtering;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;

namespace DataMorph.App.Cli;

internal struct JsonLinesRecordReader(
    RowIndexer rowIndexer,
    RowReader rowReader,
    TableSchema inputSchema,
    BatchOutputSchema outputSchema) : IRecordReader
{
    private readonly Memory<byte>[] _columnNameUtf8Bytes = [.. outputSchema.Columns
        .Select(c => Encoding.UTF8.GetBytes(c.SourceName).AsMemory())];

    private readonly Dictionary<int, ReadOnlyMemory<byte>> _filterIndexToNameBytes = inputSchema.Columns
        .ToDictionary(c => c.ColumnIndex, c => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(c.Name));

    private readonly IReadOnlyList<Engine.Filtering.FilterSpec> _filters = outputSchema.Filters;

    private readonly RowIndexer _rowIndexer = rowIndexer;
    private RowReader? _rowReader = rowReader;
    private long _batchStart;
    private IReadOnlyList<ReadOnlyMemory<byte>> _currentBatch = [];
    private int _batchIndex = -1;
    private ReadOnlyMemory<byte> _currentLineBytes = default;
    private bool _disposed;

    public ValueTask<bool> MoveNextAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_rowReader is null)
        {
            return new ValueTask<bool>(false);
        }

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

    public readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public readonly bool EvaluateFilters()
    {
        ThrowIfDisposed();
        return FilterEvaluator.EvaluateJsonFilters(_currentLineBytes, (IReadOnlyList<FilterSpec>)_filters, _filterIndexToNameBytes);
    }

    public readonly ReadOnlySpan<char> GetCellSpan(int outputColumnIndex)
    {
        ThrowIfDisposed();
        var columnNameSpan = _columnNameUtf8Bytes[outputColumnIndex].Span;
        var value = CellExtractor.ExtractCell(_currentLineBytes.Span, columnNameSpan);
        return value.AsSpan();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _rowReader?.Dispose();
        _rowReader = null;
        _disposed = true;
    }
}
