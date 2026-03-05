using System.Globalization;
using System.Text;
using System.Text.Json;
using DataMorph.Engine;

namespace DataMorph.App.Cli;

internal struct JsonLinesRecordWriter : IRecordWriter, IDisposable, IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly BatchOutputSchema _outputSchema;
    private readonly byte[] _buffer;
    private readonly MemoryStream _ms;
    private readonly Utf8JsonWriter _jsonWriter;

    public JsonLinesRecordWriter(StreamWriter writer, byte[] buffer, MemoryStream ms, Utf8JsonWriter jsonWriter, BatchOutputSchema outputSchema)
    {
        _writer = writer;
        _buffer = buffer;
        _ms = ms;
        _jsonWriter = jsonWriter;
        _outputSchema = outputSchema;
    }

    public ValueTask WriteHeaderAsync(CancellationToken ct)
    {
        return default;
    }

    public ValueTask WriteStartRecordAsync(CancellationToken ct)
    {
        _ms.SetLength(0);
        _jsonWriter.Reset(_ms);
        _jsonWriter.WriteStartObject();
        return default;
    }

    public void WriteCellSpan(int outputColumnIndex, ReadOnlySpan<char> value)
    {
        var colName = _outputSchema.Columns[outputColumnIndex].OutputName;
        _jsonWriter.WritePropertyName(colName);
        WriteJsonValue(_jsonWriter, value);
    }

    public async ValueTask WriteEndRecordAsync(CancellationToken ct)
    {
        _jsonWriter.WriteEndObject();
        await _jsonWriter.FlushAsync(ct).ConfigureAwait(false);

        var jsonLine = Encoding.UTF8.GetString(_buffer, 0, (int)_ms.Position);
        await _writer.WriteLineAsync(jsonLine.AsMemory(), ct).ConfigureAwait(false);
    }

    public async ValueTask FlushAsync(CancellationToken ct)
    {
        await _writer.FlushAsync(ct).ConfigureAwait(false);
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            writer.WriteStringValue(string.Empty);
            return;
        }

        if (value.SequenceEqual("<null>"))
        {
            writer.WriteNullValue();
            return;
        }

        if (value.SequenceEqual("<error>"))
        {
            writer.WriteStringValue(string.Empty);
            return;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            writer.WriteBooleanValue(boolValue);
            return;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            writer.WriteNumberValue(longValue);
            return;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
        {
            writer.WriteNumberValue(doubleValue);
            return;
        }

        writer.WriteStringValue(value);
    }

    public void Dispose()
    {
        _jsonWriter?.Dispose();
        _ms?.Dispose();
        _writer?.Dispose();
        if (_buffer is not null)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsonWriter is not null)
        {
            await _jsonWriter.DisposeAsync().ConfigureAwait(false);
        }
        if (_ms is not null)
        {
            await _ms.DisposeAsync().ConfigureAwait(false);
        }
        if (_writer is not null)
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
        if (_buffer is not null)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}
