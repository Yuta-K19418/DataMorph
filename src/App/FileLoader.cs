using DataMorph.App.Schema.Csv;
using DataMorph.Engine;
using DataMorph.Engine.IO.Csv;
using DataMorph.Engine.IO.JsonLines;
using JsonLinesSchema = DataMorph.App.Schema.JsonLines;

namespace DataMorph.App;

/// <summary>
/// Handles file loading and Engine object construction for CSV and JSON Lines files.
/// Updates <see cref="AppState"/> with the loaded data and returns a <see cref="Result"/>
/// indicating success or the reason for failure.
/// </summary>
internal sealed class FileLoader : IDisposable
{
    private readonly AppState _state;
    private bool _disposed;
    private CancellationTokenSource? _buildIndexCts;
    private Task _buildIndexTask = Task.CompletedTask;

    internal FileLoader(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>
    /// Detects the file format by extension and loads the file into <see cref="AppState"/>.
    /// </summary>
    /// <param name="filePath">The absolute path to the file to load.</param>
    /// <returns>
    /// <see cref="Results.Success()"/> on success, or <see cref="Results.Failure(string)"/>
    /// with a human-readable message when the file is missing, empty, unsupported, or malformed.
    /// </returns>
    internal async ValueTask<Result> LoadAsync(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        _state.CurrentFilePath = filePath;
        _state.ActionStack = [];

        if (!File.Exists(filePath))
        {
            return Results.Failure("File does not exist");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            return Results.Failure("File is empty");
        }

        if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadCsvAsync(filePath);
        }

        if (filePath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            return await LoadJsonLinesAsync(filePath);
        }

        return Results.Failure($"Unsupported file format: {Path.GetExtension(filePath)}");
    }

    private async ValueTask CancelPreviousBuildIndexAsync()
    {
        if (_buildIndexCts is not null)
        {
            await _buildIndexCts.CancelAsync().ConfigureAwait(false);
        }
        try
        {
            await _buildIndexTask.ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Swallowing old task exceptions is intentional to avoid cascading failures.
        catch (Exception)
        {
            // Expected (e.g., OperationCanceledException, IOException); swallow.
        }
#pragma warning restore CA1031
    }

    private async ValueTask<Result> LoadCsvAsync(string filePath)
    {
        await CancelPreviousBuildIndexAsync();

        _buildIndexCts?.Dispose();
        _buildIndexCts = new CancellationTokenSource();
        var ct = _buildIndexCts.Token;

        var indexer = new DataRowIndexer(filePath);
        var schemaScanner = new IncrementalSchemaScanner(filePath);

        _buildIndexTask = Task.Run(() => indexer.BuildIndex(ct));

        try
        {
            var schema = await schemaScanner.InitialScanAsync();

            if (schema.Columns.Count == 0)
            {
                return Results.Failure("File contains no data");
            }

            _state.Schema = schema;
            _state.CsvIndexer = indexer;
            _state.CsvSchemaScanner = schemaScanner;
            _state.CurrentMode = ViewMode.CsvTable;

            _ = schemaScanner
                .StartBackgroundScanAsync(schema, _state.Cts.Token)
                .ContinueWith(
                    t =>
                    {
                        if (!t.IsCompletedSuccessfully)
                        {
                            return;
                        }

                        _state.Schema = t.Result;
                    },
                    TaskScheduler.Default
                );

            return Results.Success();
        }
        catch (ArgumentException ex)
        {
            return Results.Failure(ex.Message);
        }
        catch (IOException ex)
        {
            return Results.Failure($"Error reading CSV file: {ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            return Results.Failure($"Invalid CSV format: {ex.Message}");
        }
    }

    private async ValueTask<Result> LoadJsonLinesAsync(string filePath)
    {
        await CancelPreviousBuildIndexAsync();

        _buildIndexCts?.Dispose();
        _buildIndexCts = new CancellationTokenSource();
        var ct = _buildIndexCts.Token;

        RowIndexer indexer;
        try
        {
            indexer = new RowIndexer(filePath);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return Results.Failure(ex.Message);
        }

        var tcs = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        indexer.FirstCheckpointReached += () => tcs.TrySetResult();

        _buildIndexTask = Task.Run(() => indexer.BuildIndex(ct));

        // Wait until at least 1,000 rows are indexed (or file is empty/cancelled).
        await tcs.Task.ConfigureAwait(false);

        // If cancelled before the view is set up, abort silently.
        if (ct.IsCancellationRequested)
        {
            return Results.Failure("Load cancelled.");
        }

        _state.JsonLinesIndexer = indexer;
        _state.JsonLinesSchemaScanner = null;
        _state.Schema = null;
        _state.OnSchemaRefined = null;
        _state.CurrentMode = ViewMode.JsonLinesTree;

        return Results.Success();
    }

    /// <summary>
    /// Toggles the JSON Lines display mode between Tree and Table.
    /// Performs a lazy schema scan on the first switch to Table mode.
    /// </summary>
    /// <returns>
    /// <see cref="Results.Success()"/> on success or no-op, or <see cref="Results.Failure(string)"/>
    /// when the schema scan fails.
    /// </returns>
    internal async ValueTask<Result> ToggleJsonLinesModeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state.CurrentMode == ViewMode.JsonLinesTable)
        {
            _state.CurrentMode = ViewMode.JsonLinesTree;
            return Results.Success();
        }

        if (_state.CurrentMode != ViewMode.JsonLinesTree)
        {
            return Results.Success();
        }

        if (_state.JsonLinesIndexer is null)
        {
            return Results.Success();
        }

        // Subsequent switch: reuse cached schema
        if (_state.JsonLinesSchemaScanner is not null && _state.Schema is not null)
        {
            _state.CurrentMode = ViewMode.JsonLinesTable;
            return Results.Success();
        }

        // First switch: scan schema lazily
        var scanner = new JsonLinesSchema.IncrementalSchemaScanner(_state.CurrentFilePath);

        try
        {
            var schema = await scanner.InitialScanAsync();
            _state.Schema = schema;
            _state.JsonLinesSchemaScanner = scanner;
            _state.CurrentMode = ViewMode.JsonLinesTable;

            _ = scanner
                .StartBackgroundScanAsync(schema, _state.Cts.Token)
                .ContinueWith(
                    t =>
                    {
                        if (!t.IsCompletedSuccessfully)
                        {
                            return;
                        }

                        _state.Schema = t.Result;
                        _state.OnSchemaRefined?.Invoke(t.Result);
                    },
                    TaskScheduler.Default
                );

            return Results.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Failure(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            return Results.Failure($"Invalid JSON Lines format: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _buildIndexCts?.Cancel();
        _buildIndexCts?.Dispose();
        _disposed = true;
        _state.Cts.Cancel();
    }
}
