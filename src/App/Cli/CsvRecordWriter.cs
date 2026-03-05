using System.Text;
using DataMorph.Engine;

namespace DataMorph.App.Cli;

internal struct CsvRecordWriter : IRecordWriter, IDisposable, IAsyncDisposable
{
    private readonly BatchOutputSchema _outputSchema;
    private readonly StringBuilder _sb;
    private StreamWriter? _writer;
    private bool _disposed;

    public CsvRecordWriter(StreamWriter writer, BatchOutputSchema outputSchema)
    {
        _writer = writer;
        _outputSchema = outputSchema;
        _sb = new StringBuilder();
        _disposed = false;
    }

    public async ValueTask WriteHeaderAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_writer is null)
        {
            return;
        }

        for (var i = 0; i < _outputSchema.Columns.Count; i++)
        {
            if (i > 0)
            {
                await _writer.WriteAsync(",".AsMemory(), ct).ConfigureAwait(false);
            }

            var col = _outputSchema.Columns[i];
            var escaped = CsvEscaper.EscapeCsvValue(col.OutputName);
            await _writer.WriteAsync(escaped.AsMemory(), ct).ConfigureAwait(false);
        }

        await _writer.WriteLineAsync(string.Empty.AsMemory(), ct).ConfigureAwait(false);
    }

    public ValueTask WriteStartRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        _sb.Clear();
        return default;
    }

    public void WriteCellSpan(int outputColumnIndex, ReadOnlySpan<char> value)
    {
        ThrowIfDisposed();
        if (outputColumnIndex > 0)
        {
            _sb.Append(',');
        }

        if (value.SequenceEqual("<null>") || value.SequenceEqual("<error>"))
        {
            // Empty
        }
        else if (value.Length > 0)
        {
            CsvEscaper.EscapeCsvValueToBuilder(value, _sb);
        }
    }

    public async ValueTask WriteEndRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_writer is null)
        {
            return;
        }

        await _writer.WriteLineAsync(_sb.ToString().AsMemory(), ct).ConfigureAwait(false);
    }

    public async ValueTask FlushAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_writer is null)
        {
            return;
        }
        await _writer.FlushAsync(ct).ConfigureAwait(false);
    }

    public readonly void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writer?.Dispose();
        _writer = null;
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_writer is not null)
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
            _writer = null;
        }

        _disposed = true;
    }
}
