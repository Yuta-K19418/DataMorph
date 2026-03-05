using System.Globalization;
using System.Text;
using System.Text.Json;
using DataMorph.Engine;

namespace DataMorph.App.Cli;

internal struct JsonLinesRecordWriter : IRecordWriter, IDisposable, IAsyncDisposable
{
    private readonly BatchOutputSchema _outputSchema;
    private StreamWriter? _writer;
    private byte[]? _buffer;
    private MemoryStream? _ms;
    private Utf8JsonWriter? _jsonWriter;
    private bool _disposed;

    public JsonLinesRecordWriter(StreamWriter writer, byte[] buffer, MemoryStream ms, Utf8JsonWriter jsonWriter, BatchOutputSchema outputSchema)
    {
        _writer = writer;
        _buffer = buffer;
        _ms = ms;
        _jsonWriter = jsonWriter;
        _outputSchema = outputSchema;
        _disposed = false;
    }

    public ValueTask WriteHeaderAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        return default;
    }

    public ValueTask WriteStartRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_ms is null || _jsonWriter is null)
        {
            return default;
        }

        _ms.SetLength(0);
        _jsonWriter.Reset(_ms);
        _jsonWriter.WriteStartObject();
        return default;
    }

    public void WriteCellSpan(int outputColumnIndex, ReadOnlySpan<char> value)
    {
        ThrowIfDisposed();
        if (_jsonWriter is null)
        {
            return;
        }

        var colName = _outputSchema.Columns[outputColumnIndex].OutputName;
        _jsonWriter.WritePropertyName(colName);
        WriteJsonValue(_jsonWriter, value);
    }

    public async ValueTask WriteEndRecordAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_jsonWriter is null || _ms is null || _writer is null || _buffer is null)
        {
            return;
        }

        _jsonWriter.WriteEndObject();
        await _jsonWriter.FlushAsync(ct).ConfigureAwait(false);

        var jsonLine = Encoding.UTF8.GetString(_buffer, 0, (int)_ms.Position);
        await _writer.WriteLineAsync(jsonLine.AsMemory(), ct).ConfigureAwait(false);
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

        _jsonWriter?.Dispose();
        _jsonWriter = null;
        _ms?.Dispose();
        _ms = null;
        _writer?.Dispose();
        _writer = null;
        if (_buffer is not null)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_jsonWriter is not null)
        {
            await _jsonWriter.DisposeAsync().ConfigureAwait(false);
            _jsonWriter = null;
        }
        if (_ms is not null)
        {
            await _ms.DisposeAsync().ConfigureAwait(false);
            _ms = null;
        }
        if (_writer is not null)
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
            _writer = null;
        }
        if (_buffer is not null)
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
        _disposed = true;
    }
}
