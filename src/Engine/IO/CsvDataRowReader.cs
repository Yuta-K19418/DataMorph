using nietras.SeparatedValues;

namespace DataMorph.Engine.IO;

/// <summary>
/// Low-level CSV row reader that reads raw CSV data from a file stream.
/// Returns rows as read-only lists of ReadOnlyMemory for memory efficiency.
/// </summary>
public sealed class CsvDataRowReader(string filePath, int columnCount)
{
    private readonly string _filePath = filePath;
    private readonly int _columnCount = columnCount;

    /// <summary>
    /// Reads a specified number of rows from the CSV file starting at the given byte offset.
    /// </summary>
    /// <param name="byteOffset">The byte offset in the file to start reading from.</param>
    /// <param name="rowsToSkip">Number of rows to skip after seeking to the byte offset.</param>
    /// <param name="rowsToRead">Maximum number of rows to read.</param>
    /// <returns>A list of CSV rows.</returns>
    public IReadOnlyList<CsvDataRow> ReadRows(long byteOffset, int rowsToSkip, int rowsToRead)
    {
        if (byteOffset < 0)
        {
            return [];
        }

        if (rowsToRead <= 0)
        {
            return [];
        }

        var rows = new List<CsvDataRow>(rowsToRead);

        try
        {
            using var fileStream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );

            fileStream.Seek(byteOffset, SeekOrigin.Begin);

            using var reader = Sep.New(',').Reader(o => o with { HasHeader = false }).From(fileStream);

            // Skip rows until the actual start row
            var skipped = 0;
            while (skipped < rowsToSkip && reader.MoveNext())
            {
                skipped++;
            }

            // Read the requested number of rows
            var readCount = 0;
            while (readCount < rowsToRead && reader.MoveNext())
            {
                var record = reader.Current;

                // Calculate total buffer size needed
                var totalLength = 0;
                for (var i = 0; i < _columnCount; i++)
                {
                    if (i < record.ColCount)
                    {
                        totalLength += record[i].Span.Length;
                    }
                }

                // Allocate single buffer for all columns
                var buffer = new char[totalLength];
                var columns = new ReadOnlyMemory<char>[_columnCount];
                var bufferPos = 0;

                // Copy column data to buffer and create ReadOnlyMemory slices
                for (var i = 0; i < _columnCount; i++)
                {
                    if (i < record.ColCount)
                    {
                        var colSpan = record[i].Span;
                        colSpan.CopyTo(buffer.AsSpan(bufferPos, colSpan.Length));
                        columns[i] = new ReadOnlyMemory<char>(buffer, bufferPos, colSpan.Length);
                        bufferPos += colSpan.Length;
                    }
                    else
                    {
                        // Empty column
                        columns[i] = ReadOnlyMemory<char>.Empty;
                    }
                }

                rows.Add(columns);
                readCount++;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            // Return empty list on I/O errors
            return [];
        }

        return rows;
    }
}
