using System.Text;
using Refedle.Engine;
using Refedle.Engine.IO.JsonLines;
using Refedle.Engine.Models;

namespace Refedle.App.Cli;

internal struct JsonLinesRecordReader : IRecordReader
{
    private readonly RowIndexer _rowIndexer;
#pragma warning disable IDE0052, S1450 // Read in Step 2 (GetCellData property-name lookup); restored then.
    private readonly Memory<byte>[] _columnNameUtf8Bytes;
#pragma warning restore IDE0052, S1450
    private readonly Dictionary<int, ReadOnlyMemory<byte>> _filterIndexToNameBytes;
    private readonly IReadOnlyList<Engine.Filtering.FilterSpec> _filters;
    private RowReader? _rowReader;
    private long _batchStart;
    private IReadOnlyList<JsonRawBytes> _currentBatch;
    private int _batchIndex;
    private JsonRawBytes _currentLineBytes;
    private bool _disposed;

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
        _disposed = false;
    }

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

            _currentBatch = _rowReader.ReadLines(byteOffset, rowOffset, linesToRead);
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
        return FilterEvaluator.EvaluateJsonFilters(_currentLineBytes, _filters, _filterIndexToNameBytes);
    }

    public readonly CellData GetCellData(int outputColumnIndex)
    {
        ThrowIfDisposed();
        // Step 2: scan _currentLineBytes with a local Utf8JsonReader for the property named
        // _columnNameUtf8Bytes[outputColumnIndex]; derive Presence/Encoding/Value from JsonTokenType.
        throw new NotImplementedException();
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
