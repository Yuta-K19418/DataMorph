using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using DataMorph.Engine;

namespace DataMorph.App.Cli;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "JsonLinesRecordWriter is a struct designed for monomorphization as per ADR. It implements IRecordWriter which inherits from IDisposable and IAsyncDisposable, but CA1001 analyzer may be confused by structs or specific field types.")]
internal partial struct JsonLinesRecordWriter : IRecordWriter
{
    private const int InitialBufferSize = 1024 * 64; // 64 KB
    private readonly BatchOutputSchema _outputSchema;
    private Stream? _stream;
    private PooledBufferWriter? _bufferWriter;
    private Utf8JsonWriter? _jsonWriter;
    private bool _disposed;

    public JsonLinesRecordWriter(Stream stream, BatchOutputSchema outputSchema)
    {
        _stream = stream;
        _outputSchema = outputSchema;
        _bufferWriter = new(InitialBufferSize);
        try
        {
            _jsonWriter = new(_bufferWriter, new() { SkipValidation = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
        catch
        {
            _bufferWriter.Dispose();
            throw;
        }
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
        if (_jsonWriter is null || _bufferWriter is null)
        {
            return default;
        }

        _bufferWriter.Clear();
        _jsonWriter.Reset();
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
        if (_jsonWriter is null || _stream is null || _bufferWriter is null)
        {
            return;
        }

        _jsonWriter.WriteEndObject();

#pragma warning disable CA1849 // Flush to IBufferWriter is synchronous and fast
        _jsonWriter.Flush();
#pragma warning restore CA1849

        // Add newline (using \n as standard for JSONL across platforms)
        var span = _bufferWriter.GetSpan(1);
        span[0] = (byte)'\n';
        _bufferWriter.Advance(1);

        // Write to stream
        var memory = _bufferWriter.WrittenMemory;
        if (memory.Length > 0)
        {
            await _stream.WriteAsync(memory, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask FlushAsync(CancellationToken ct)
    {
        ThrowIfDisposed();
        if (_stream is null)
        {
            return;
        }
        await _stream.FlushAsync(ct).ConfigureAwait(false);
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
        _bufferWriter?.Dispose();
        _bufferWriter = null;
        _stream?.Dispose();
        _stream = null;
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
        _bufferWriter?.Dispose();
        _bufferWriter = null;
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }
        _disposed = true;
    }
}
