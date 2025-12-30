using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace DataMorph.Engine.IO;

/// <summary>
/// Provides SIMD-accelerated indexing of line boundaries in memory-mapped files.
/// </summary>
/// <remarks>
/// This class scans byte data to locate newline characters and builds an index
/// of row start positions, enabling O(1) random access to any line in the file.
/// Uses hardware intrinsics (SIMD) for high-performance scanning of large files.
/// </remarks>
public sealed class RowIndexer : IDisposable
{
    private readonly List<long> _rowOffsets;
    private bool _disposed;

    /// <summary>
    /// Gets the number of rows indexed.
    /// </summary>
    public int RowCount => _rowOffsets.Count;

    /// <summary>
    /// Gets the byte offset for a specific row index.
    /// </summary>
    /// <param name="rowIndex">The zero-based row index.</param>
    /// <returns>The byte offset where the row starts.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Row index is out of range.</exception>
    /// <exception cref="ObjectDisposedException">The indexer has been disposed.</exception>
    public long this[int rowIndex]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(rowIndex, _rowOffsets.Count);
            return _rowOffsets[rowIndex];
        }
    }

    private RowIndexer(List<long> rowOffsets)
    {
        _rowOffsets = rowOffsets;
    }

    /// <summary>
    /// Builds a row index from the provided memory-mapped service.
    /// </summary>
    /// <param name="mmapService">The memory-mapped file service to index.</param>
    /// <param name="chunkSize">The size of chunks to process (default: 1MB).</param>
    /// <returns>A Result containing the RowIndexer on success, or an error message on failure.</returns>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "RowIndexer ownership is transferred to the caller via Result<T>")]
    public static Result<RowIndexer> Build(MmapService mmapService, int chunkSize = 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(mmapService);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        try
        {
            var rowOffsets = new List<long> { 0 }; // First row always starts at offset 0
            var fileLength = mmapService.Length;
            var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);

            try
            {
                long currentOffset = 0;

                while (currentOffset < fileLength)
                {
                    var bytesToRead = (int)Math.Min(chunkSize, fileLength - currentOffset);
                    var span = buffer.AsSpan(0, bytesToRead);

                    mmapService.Read(currentOffset, span);

                    // Scan for newlines using SIMD acceleration
                    ScanForNewlines(span, currentOffset, rowOffsets);

                    currentOffset += bytesToRead;
                }

                return Results.Success(new RowIndexer(rowOffsets));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or ArgumentOutOfRangeException)
        {
            return Results.Failure<RowIndexer>($"Failed to build row index: {ex.Message}");
        }
    }

    /// <summary>
    /// Scans a byte span for newline characters and records their positions.
    /// Uses SIMD intrinsics when available for improved performance.
    /// </summary>
    /// <param name="data">The data to scan.</param>
    /// <param name="baseOffset">The file offset where this data chunk starts.</param>
    /// <param name="rowOffsets">The list to append discovered row offsets to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ScanForNewlines(ReadOnlySpan<byte> data, long baseOffset, List<long> rowOffsets)
    {
        var index = 0;

        // Select the appropriate scanning strategy based on hardware capabilities
        if (Vector256.IsHardwareAccelerated && data.Length >= Vector256<byte>.Count)
        {
            index = ScanWithVector256(data, baseOffset, rowOffsets, index);
        }
        else if (Vector128.IsHardwareAccelerated && data.Length >= Vector128<byte>.Count)
        {
            index = ScanWithVector128(data, baseOffset, rowOffsets, index);
        }

        // Process remaining bytes with scalar code
        ScanScalar(data, baseOffset, rowOffsets, index);
    }

    /// <summary>
    /// Scans data using 256-bit SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ScanWithVector256(ReadOnlySpan<byte> data, long baseOffset, List<long> rowOffsets, int startIndex)
    {
        const byte LineFeed = (byte)'\n';
        const byte CarriageReturn = (byte)'\r';

        var lfVector = Vector256.Create(LineFeed);
        var crVector = Vector256.Create(CarriageReturn);
        var index = startIndex;

        while (index <= data.Length - Vector256<byte>.Count)
        {
            var chunk = Vector256.Create(data.Slice(index, Vector256<byte>.Count));
            var lfMatches = Vector256.Equals(chunk, lfVector);
            var crMatches = Vector256.Equals(chunk, crVector);
            var matches = lfMatches | crMatches;

            ProcessMatchMask(data, baseOffset, rowOffsets, index, matches.ExtractMostSignificantBits(), vectorSize: 32);

            index += Vector256<byte>.Count;
        }

        return index;
    }

    /// <summary>
    /// Scans data using 128-bit SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ScanWithVector128(ReadOnlySpan<byte> data, long baseOffset, List<long> rowOffsets, int startIndex)
    {
        const byte LineFeed = (byte)'\n';
        const byte CarriageReturn = (byte)'\r';

        var lfVector = Vector128.Create(LineFeed);
        var crVector = Vector128.Create(CarriageReturn);
        var index = startIndex;

        while (index <= data.Length - Vector128<byte>.Count)
        {
            var chunk = Vector128.Create(data.Slice(index, Vector128<byte>.Count));
            var lfMatches = Vector128.Equals(chunk, lfVector);
            var crMatches = Vector128.Equals(chunk, crVector);
            var matches = lfMatches | crMatches;

            ProcessMatchMask(data, baseOffset, rowOffsets, index, matches.ExtractMostSignificantBits(), vectorSize: 16);

            index += Vector128<byte>.Count;
        }

        return index;
    }

    /// <summary>
    /// Processes the bitmask of newline matches from SIMD operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ProcessMatchMask(ReadOnlySpan<byte> data, long baseOffset, List<long> rowOffsets, int index, uint mask, int vectorSize)
    {
        const byte LineFeed = (byte)'\n';
        const byte CarriageReturn = (byte)'\r';

        while (mask != 0)
        {
            var bitPos = BitOperations.TrailingZeroCount(mask);
            var absolutePos = baseOffset + index + bitPos;
            mask &= ~(1u << bitPos);

            // Handle CRLF: if we found CR and next byte is LF, skip the LF
            if (data[index + bitPos] == CarriageReturn &&
                index + bitPos + 1 < data.Length &&
                data[index + bitPos + 1] == LineFeed)
            {
                rowOffsets.Add(absolutePos + 2);
                if (bitPos + 1 < vectorSize)
                {
                    mask &= ~(1u << (bitPos + 1));
                }
                continue;
            }

            // Normal newline (LF or standalone CR)
            rowOffsets.Add(absolutePos + 1);
        }
    }

    /// <summary>
    /// Scans data using scalar (non-SIMD) code for remaining bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ScanScalar(ReadOnlySpan<byte> data, long baseOffset, List<long> rowOffsets, int startIndex)
    {
        const byte LineFeed = (byte)'\n';
        const byte CarriageReturn = (byte)'\r';

        var index = startIndex;

        while (index < data.Length)
        {
            if (data[index] == LineFeed)
            {
                rowOffsets.Add(baseOffset + index + 1);
                index++;
                continue;
            }

            if (data[index] != CarriageReturn)
            {
                index++;
                continue;
            }

            // Handle CRLF
            if (index + 1 < data.Length && data[index + 1] == LineFeed)
            {
                rowOffsets.Add(baseOffset + index + 2);
                index += 2; // Skip the LF
                continue;
            }

            rowOffsets.Add(baseOffset + index + 1);
            index++;
        }
    }

    /// <summary>
    /// Releases resources used by the indexer.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _rowOffsets.Clear();
        _disposed = true;
    }
}
