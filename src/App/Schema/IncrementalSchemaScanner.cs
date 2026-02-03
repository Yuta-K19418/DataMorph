using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;
using nietras.SeparatedValues;

namespace DataMorph.App.Schema;

/// <summary>
/// Performs incremental schema inference for CSV files.
/// - Initial scan: first 200 rows (synchronous)
/// - Background scan: remaining rows (asynchronous)
/// - Thread-safe schema updates via Copy-on-Write
/// </summary>
internal sealed class IncrementalSchemaScanner
{
    private const int InitialScanCount = 200;
    private const int BackgroundBatchSize = 1000;

    private readonly string _filePath;

    /// <summary>
    /// Creates a new incremental schema scanner.
    /// </summary>
    /// <param name="filePath">Path to the CSV file.</param>
    public IncrementalSchemaScanner(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>
    /// Performs initial scan on first 200 rows synchronously.
    /// Must be awaited before UI can display schema.
    /// </summary>
    public async Task<TableSchema> InitialScanAsync()
    {
        return await Task.Run(() =>
        {
            var rows = ReadRows(0, InitialScanCount);
            var columnNames = ReadColumnNames();
            var scanResult = CsvSchemaScanner.ScanSchema(columnNames, rows, InitialScanCount);

            if (scanResult.IsFailure)
            {
                throw new InvalidOperationException(scanResult.Error);
            }

            return scanResult.Value;
        });
    }

    /// <summary>
    /// Starts background scan from row 201 onwards.
    /// Fire-and-forget - returns Task but should not be awaited.
    /// </summary>
    public Task<TableSchema> StartBackgroundScanAsync(
        TableSchema currentSchema,
        CancellationToken cancellationToken
    )
    {
        return Task.Run(
            () =>
            {
                try
                {
                    return ProcessRemainingRows(currentSchema, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelled
                    return currentSchema;
                }
            },
            cancellationToken
        );
    }

    private TableSchema ProcessRemainingRows(TableSchema currentSchema, CancellationToken token)
    {
        var rowIndex = InitialScanCount;
        var refinedSchema = currentSchema;

        while (!token.IsCancellationRequested)
        {
            var rows = ReadRows(rowIndex, BackgroundBatchSize);
            if (rows.Count == 0)
            {
                break;
            }

            foreach (var row in rows)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var refineResult = CsvSchemaScanner.RefineSchema(refinedSchema, row);
                if (refineResult.IsSuccess)
                {
                    refinedSchema = refineResult.Value;
                }
            }

            rowIndex += rows.Count;
        }

        return refinedSchema;
    }

    private List<CsvDataRow> ReadRows(int startRow, int count)
    {
        var rows = new List<CsvDataRow>(count);

        using var reader = Sep.New(',').Reader().FromFile(_filePath);

        // Skip to start row (0-based, where 0 means first data row after header)
        var currentRow = 0;
        while (currentRow < startRow && reader.MoveNext())
        {
            currentRow++;
        }

        // Read requested rows
        var readCount = 0;
        while (readCount < count && reader.MoveNext())
        {
            var record = reader.Current;
            var columns = new ReadOnlyMemory<char>[record.ColCount];

            for (var i = 0; i < record.ColCount; i++)
            {
                var columnSpan = record[i].Span;
                if (columnSpan.Length > 0)
                {
                    // Copy span to memory (necessary for CsvDataRow which expects ReadOnlyMemory<char>)
                    columns[i] = columnSpan.ToArray().AsMemory();
                    continue;
                }

                columns[i] = ReadOnlyMemory<char>.Empty;
            }

            rows.Add(columns);
            readCount++;
        }

        return rows;
    }

    private string[] ReadColumnNames()
    {
        using var reader = Sep.New(',').Reader().FromFile(_filePath);

        // Header column names are automatically available
        var header = reader.Header;
        if (header.ColNames.Count == 0)
        {
            throw new InvalidOperationException("CSV file has no columns.");
        }

        // Process column names
        var processedNames = new string[header.ColNames.Count];
        for (var i = 0; i < header.ColNames.Count; i++)
        {
            var columnName = header.ColNames[i];
            if (string.IsNullOrWhiteSpace(columnName))
            {
                columnName = $"Column{i + 1}";
            }
            processedNames[i] = columnName;
        }

        return processedNames;
    }
}

