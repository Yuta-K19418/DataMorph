using System.Buffers;
using System.Text.Json;

namespace DataMorph.Engine.IO.JsonArray;

/// <summary>
/// Reads raw JSON element bytes from a JSON Array file given a checkpoint byte offset.
/// Uses <see cref="MmapService"/> for consistent memory-mapped random access and
/// <see cref="System.Text.Json.Utf8JsonReader"/> to locate element boundaries.
/// </summary>
public sealed class ElementReader : IDisposable
{
    private readonly MmapService _mmap;
    private bool _disposed;

    private const int BufferSize = 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElementReader"/> class.
    /// </summary>
    /// <param name="filePath">Path to the JSON Array file.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the file cannot be memory-mapped.</exception>
    public ElementReader(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var mmapResult = MmapService.Open(filePath);
        if (mmapResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to open memory-mapped file: {mmapResult.Error.ToString()}"
            );
        }

        _mmap = mmapResult.Value;
    }

    /// <summary>
    /// Reads raw JSON bytes for elements starting at <paramref name="byteOffset"/>.
    /// </summary>
    /// <param name="byteOffset">A checkpoint byte offset from <see cref="RowIndexer.GetCheckPoint"/>.</param>
    /// <param name="elementsToSkip">Number of elements to skip before collecting (non-negative).</param>
    /// <param name="elementsToFetch">Maximum elements to collect after skipping (non-negative).</param>
    /// <returns>Raw JSON bytes per element (NOT TreeNode — no App layer dependency).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for negative <paramref name="elementsToSkip"/> or <paramref name="elementsToFetch"/>.</exception>
    /// <exception cref="ObjectDisposedException">Thrown after <see cref="Dispose"/> has been called.</exception>
    public IReadOnlyList<JsonRawBytes> ReadElements(
        long byteOffset,
        int elementsToSkip,
        int elementsToFetch
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(elementsToSkip);
        ArgumentOutOfRangeException.ThrowIfNegative(elementsToFetch);

        if (elementsToFetch == 0 || byteOffset < 0)
        {
            return [];
        }

        List<JsonRawBytes> result = [];
        result.EnsureCapacity(elementsToFetch);

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            FetchElements(buffer, byteOffset, elementsToSkip, elementsToFetch, result);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return result;
    }

    private void FetchElements(
        byte[] buffer,
        long byteOffset,
        int elementsToSkip,
        int elementsToFetch,
        List<JsonRawBytes> result)
    {
        var state = default(JsonReaderState);
        // bufferOriginFileOffset maps buffer[0] → virtual file offset (byteOffset - 1 for synthetic '[').
        var bufferOriginFileOffset = byteOffset - 1L;
        var fileReadOffset = byteOffset;
        var remainingLen = 0;
        var elementsEncountered = 0;
        var currentElementStartFile = -1L;
        var firstFill = true;

        while (result.Count < elementsToFetch)
        {
            if (remainingLen == BufferSize)
            {
                throw new NotSupportedException("JSON element exceeds maximum supported size.");
            }

            int dataEnd;

            if (firstFill)
            {
                firstFill = false;
                buffer[0] = (byte)'[';
                var available = _mmap.Length - fileReadOffset;
                var toRead = (int)Math.Min(BufferSize - 1, Math.Max(0L, available));
                if (toRead > 0)
                {
                    _mmap.Read(fileReadOffset, buffer.AsSpan(1, toRead));
                }

                fileReadOffset += toRead;
                dataEnd = 1 + toRead;
            }
            else
            {
                var available = _mmap.Length - fileReadOffset;
                var toRead = (int)Math.Min(BufferSize - remainingLen, Math.Max(0L, available));
                if (toRead > 0)
                {
                    _mmap.Read(fileReadOffset, buffer.AsSpan(remainingLen, toRead));
                }

                fileReadOffset += toRead;
                dataEnd = remainingLen + toRead;
            }

            var isFinalBlock = fileReadOffset >= _mmap.Length;

            if (dataEnd == 0)
            {
                break;
            }

            var reader = new Utf8JsonReader(buffer.AsSpan(0, dataEnd), isFinalBlock, state);
            var rootDone = false;

            while (reader.Read())
            {
                if (reader.CurrentDepth == 0 && reader.TokenType == JsonTokenType.EndArray)
                {
                    rootDone = true;
                    break;
                }

                if (reader.CurrentDepth != 1)
                {
                    continue;
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    currentElementStartFile = bufferOriginFileOffset + reader.TokenStartIndex;
                    continue;
                }

                if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                {
                    var endFile = bufferOriginFileOffset + reader.BytesConsumed;
                    elementsEncountered = AppendElement(currentElementStartFile, endFile, elementsEncountered, elementsToSkip, result);
                    currentElementStartFile = -1L;
                    if (result.Count >= elementsToFetch)
                    {
                        break;
                    }

                    continue;
                }

                // Primitive element at depth 1 (number, string, bool, null).
                elementsEncountered = AppendElement(
                    bufferOriginFileOffset + reader.TokenStartIndex,
                    bufferOriginFileOffset + reader.BytesConsumed,
                    elementsEncountered, elementsToSkip, result);
                if (result.Count >= elementsToFetch)
                {
                    break;
                }
            }

            if (rootDone || result.Count >= elementsToFetch || isFinalBlock)
            {
                break;
            }

            state = reader.CurrentState;
            var consumed = (int)reader.BytesConsumed;
            bufferOriginFileOffset += consumed;
            remainingLen = dataEnd - consumed;

            if (remainingLen > 0 && consumed > 0)
            {
                buffer.AsSpan(consumed, remainingLen).CopyTo(buffer);
            }
        }
    }

    private int AppendElement(
        long startFile,
        long endFile,
        int encountered,
        int toSkip,
        List<JsonRawBytes> result)
    {
        encountered++;
        if (encountered <= toSkip)
        {
            return encountered;
        }

        var length = (int)(endFile - startFile);
        if (length <= 0)
        {
            return encountered;
        }

        var bytes = new byte[length];
        _mmap.Read(startFile, bytes);
        result.Add(bytes.AsMemory());
        return encountered;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mmap.Dispose();
        _disposed = true;
    }
}
