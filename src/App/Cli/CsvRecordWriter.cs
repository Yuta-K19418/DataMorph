using System.Text;
using DataMorph.Engine;

namespace DataMorph.App.Cli;

internal struct CsvRecordWriter : IRecordWriter, IDisposable, IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly BatchOutputSchema _outputSchema;
    private readonly StringBuilder _sb;

    public CsvRecordWriter(StreamWriter writer, BatchOutputSchema outputSchema)
    {
        _writer = writer;
        _outputSchema = outputSchema;
        _sb = new StringBuilder();
    }

    public async ValueTask WriteHeaderAsync(CancellationToken ct)
    {
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
        _sb.Clear();
        return default;
    }

    public void WriteCellSpan(int outputColumnIndex, ReadOnlySpan<char> value)
    {
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
        await _writer.WriteLineAsync(_sb.ToString().AsMemory(), ct).ConfigureAwait(false);
    }

    public async ValueTask FlushAsync(CancellationToken ct)
    {
        await _writer.FlushAsync(ct).ConfigureAwait(false);
    }


    public void Dispose()
    {
        _writer?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_writer is not null)
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
    }
}
