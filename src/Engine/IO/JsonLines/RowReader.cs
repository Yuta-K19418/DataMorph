using System.Buffers;
using System.Text.Json;

namespace DataMorph.Engine.IO.JsonLines;

/// <summary>
/// Reads JSON line bytes from a file using indexed byte offsets.
/// Returns raw JSON bytes per line (not TreeNode) to maintain Engine/App layer separation.
/// </summary>
public sealed class RowReader : IDisposable
{
    private readonly MmapService _mmap;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RowReader"/> class.
    /// </summary>
    /// <param name="filePath">Path to the JSON Lines file.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="filePath"/> is null.</exception>
    public RowReader(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var mmapResult = MmapService.Open(filePath);
        if (!mmapResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Failed to open memory-mapped file: {mmapResult.Error.ToString()}"
            );
        }

        _mmap = mmapResult.Value;
    }

    /// <summary>
    /// Reads raw JSON line bytes for a specified range.
    /// </summary>
    /// <param name="byteOffset">Starting byte offset in the file.</param>
    /// <param name="linesToSkip">Number of lines to skip from the offset.</param>
    /// <param name="linesToRead">Maximum number of lines to read.</param>
    /// <returns>A list of raw JSON line bytes.</returns>
    /// <exception cref="ObjectDisposedException">The reader has been disposed.</exception>
    /// <exception cref="InvalidDataException">The JSON line data is invalid.</exception>
    public IReadOnlyList<ReadOnlyMemory<byte>> ReadLineBytes(
        long byteOffset,
        int linesToSkip,
        int linesToRead
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = new List<ReadOnlyMemory<byte>>(linesToRead);
        var currentOffset = byteOffset;
        var skipped = 0;

        var scanner = new RowScanner();

        // Skip lines if needed
        while (skipped < linesToSkip)
        {
            var (lineCompleted, bytesConsumed) = FindNextLineLength(currentOffset, ref scanner);
            // bytesConsumed <= 0 indicates EOF or error
            if (bytesConsumed <= 0)
            {
                // No more data to skip - when trying to skip beyond EOF, there are no lines to read
                return result;
            }

            currentOffset += bytesConsumed;

            // Only increment skip count when a complete line is found
            if (lineCompleted)
            {
                skipped++;
            }
            // Incomplete lines (spanning buffers) continue processing
            // without incrementing skip count
        }

        // Read requested lines
        var linesRead = 0;
        var incompleteLineBytes = 0; // Track cumulative bytes of incomplete lines

        while (linesRead < linesToRead)
        {
            var (lineCompleted, bytesConsumed) = FindNextLineLength(
                currentOffset + incompleteLineBytes,
                ref scanner
            );
            if (bytesConsumed <= 0)
            {
                // No more data
                return result;
            }

            if (!lineCompleted)
            {
                incompleteLineBytes += bytesConsumed;
                continue;
            }

            // totalLineBytes includes both the complete line and any preceding incomplete bytes
            var totalLineBytes = bytesConsumed + incompleteLineBytes;

            // Read the line bytes (complete line plus any preceding incomplete bytes)
            var lineBytes = new byte[totalLineBytes];

            // Start reading from the beginning of the incomplete line (if any)
            _mmap.Read(currentOffset, lineBytes);

            // Remove trailing newline characters
            var trimmedSpan = TrimNewline(lineBytes.AsSpan());

            // Validate only if there is content
            if (trimmedSpan.Length > 0)
            {
                ValidateJsonLine(trimmedSpan);
            }

            result.Add(lineBytes.AsMemory(0, trimmedSpan.Length));

            // Advance currentOffset by the total bytes consumed (complete line + incomplete bytes)
            currentOffset += totalLineBytes;

            incompleteLineBytes = 0; // Reset
            linesRead++;
        }

        return result;
    }

    private static ReadOnlySpan<byte> TrimNewline(ReadOnlySpan<byte> span)
    {
        // Remove trailing \r\n or \n
        if (span.Length > 0 && span[span.Length - 1] == '\n')
        {
            span = span[..^1];
            if (span.Length > 0 && span[span.Length - 1] == '\r')
            {
                span = span[..^1];
            }
        }
        return span;
    }

    // Removed CollectLineBytes method because it is no longer needed

    private static void ValidateJsonLine(ReadOnlySpan<byte> lineBytes)
    {
        // Skip validation for empty lines (should not happen with JSON Lines)
        if (lineBytes.Length == 0)
        {
            return;
        }

        try
        {
            var reader = new Utf8JsonReader(lineBytes);
            // Read at least one token to validate JSON structure
            // If the line is not valid JSON, Utf8JsonReader will throw
            if (!reader.Read())
            {
                // Empty JSON is not valid for JSON Lines
                throw new InvalidDataException("Empty JSON line");
            }

            // Consume the rest of the JSON to ensure it's well‑formed
            while (reader.Read())
            {
                // Just read through to validate
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Invalid JSON line at byte position {ex.BytePositionInLine}",
                ex
            );
        }
    }

    private (bool lineCompleted, int bytesConsumed) FindNextLineLength(
        long startOffset,
        ref RowScanner scanner
    )
    {
        const int maxSearch = 1024 * 1024; // Search up to 1 MB
        var remaining = _mmap.Length - startOffset;
        if (remaining <= 0)
        {
            return (false, 0);
        }

        var searchLength = (int)Math.Min(maxSearch, remaining);
        if (searchLength <= 0)
        {
            return (false, 0);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(searchLength);
        try
        {
            var span = buffer.AsSpan(0, searchLength);
            _mmap.Read(startOffset, span);

            return scanner.FindNextLineLength(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _mmap?.Dispose();
            _disposed = true;
        }
    }
}
