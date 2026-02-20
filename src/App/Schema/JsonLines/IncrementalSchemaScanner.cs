using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;

namespace DataMorph.App.Schema.JsonLines;

/// <summary>
/// Performs incremental schema inference for JSON Lines files.
/// - Initial scan: first 200 lines
/// - Background scan: remaining lines in batches of 1000
/// - Thread-safe schema updates via Copy-on-Write
/// </summary>
internal sealed class IncrementalSchemaScanner
{
    private const int InitialScanCount = 200;
    private const int BackgroundBatchSize = 1000;

    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of <see cref="IncrementalSchemaScanner"/>.
    /// </summary>
    /// <param name="filePath">Path to the JSON Lines file.</param>
    public IncrementalSchemaScanner(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Performs initial scan on first 200 lines.
    /// Reads lines directly via RowReader independent of RowIndexer to avoid race conditions.
    /// Must be awaited before UI can display schema.
    /// </summary>
    public Task<TableSchema> InitialScanAsync()
    {
        return Task.Run(() =>
        {
            using var reader = new RowReader(_filePath);
            var lines = reader.ReadLineBytes(
                byteOffset: 0,
                linesToSkip: 0,
                linesToRead: InitialScanCount
            );
            var scanResult = SchemaScanner.ScanSchema(lines, InitialScanCount);

            if (scanResult.IsFailure)
            {
                throw new InvalidOperationException(scanResult.Error);
            }

            return scanResult.Value;
        });
    }

    /// <summary>
    /// Starts background scan from line 201 onwards in batches of 1000.
    /// Calls SchemaScanner.RefineSchema() per line and returns the final refined schema.
    /// </summary>
    /// <param name="currentSchema">The schema produced by <see cref="InitialScanAsync"/>.</param>
    /// <param name="cancellationToken">Token to stop the background scan.</param>
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
                    return ProcessRemainingLines(currentSchema, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return currentSchema;
                }
            },
            cancellationToken
        );
    }

    private TableSchema ProcessRemainingLines(TableSchema currentSchema, CancellationToken token)
    {
        var lineIndex = InitialScanCount;
        var refinedSchema = currentSchema;

        while (!token.IsCancellationRequested)
        {
            using var reader = new RowReader(_filePath);
            var lines = reader.ReadLineBytes(
                byteOffset: 0,
                linesToSkip: lineIndex,
                linesToRead: BackgroundBatchSize
            );

            if (lines.Count == 0)
            {
                break;
            }

            foreach (var line in lines)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                var refineResult = SchemaScanner.RefineSchema(refinedSchema, line.Span);
                if (refineResult.IsSuccess)
                {
                    refinedSchema = refineResult.Value;
                }
            }

            lineIndex += lines.Count;
        }

        return refinedSchema;
    }
}
