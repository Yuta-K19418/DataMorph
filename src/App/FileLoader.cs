using DataMorph.App.Schema.Csv;
using DataMorph.Engine.IO.Csv;
using DataMorph.Engine.IO.JsonLines;
using JsonLinesSchema = DataMorph.App.Schema.JsonLines;

namespace DataMorph.App;

/// <summary>
/// Handles file loading and Engine object construction for CSV and JSON Lines files.
/// Updates <see cref="AppState"/> with the loaded data; has no dependency on Terminal.Gui.
/// </summary>
internal sealed class FileLoader : IDisposable
{
    private readonly AppState _state;
    private bool _disposed;

    internal FileLoader(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>
    /// Detects the file format by extension and loads the file into <see cref="AppState"/>.
    /// For unsupported formats, sets <see cref="AppState.LastError"/> and returns.
    /// </summary>
    /// <param name="filePath">The absolute path to the file to load.</param>
    internal Task LoadAsync(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        _state.CurrentFilePath = filePath;
        _state.ActionStack = [];

        if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return LoadCsvAsync(filePath);
        }

        if (filePath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            return LoadJsonLinesAsync(filePath);
        }

        _state.LastError = $"Unsupported file format: {Path.GetExtension(filePath)}";
        return Task.CompletedTask;
    }

    private async Task LoadCsvAsync(string filePath)
    {
        var indexer = new DataRowIndexer(filePath);
        _ = Task.Run(indexer.BuildIndex);

        var schemaScanner = new IncrementalSchemaScanner(filePath);

        try
        {
            var schema = await schemaScanner.InitialScanAsync();
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
        }
        catch (ArgumentException ex)
        {
            _state.LastError = ex.Message;
        }
        catch (IOException ex)
        {
            _state.LastError = $"Error reading CSV file: {ex.Message}";
        }
    }

    private Task LoadJsonLinesAsync(string filePath)
    {
        var indexer = new RowIndexer(filePath);
        _ = Task.Run(indexer.BuildIndex);

        _state.JsonLinesIndexer = indexer;
        _state.JsonLinesSchemaScanner = null;
        _state.Schema = null;
        _state.OnSchemaRefined = null;
        _state.CurrentMode = ViewMode.JsonLinesTree;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Toggles the JSON Lines display mode between Tree and Table.
    /// Performs a lazy schema scan on the first switch to Table mode.
    /// </summary>
    internal async Task ToggleJsonLinesModeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_state.CurrentMode == ViewMode.JsonLinesTable)
        {
            _state.CurrentMode = ViewMode.JsonLinesTree;
            return;
        }

        if (_state.CurrentMode != ViewMode.JsonLinesTree)
        {
            return;
        }

        if (_state.JsonLinesIndexer is null)
        {
            return;
        }

        // Subsequent switch: reuse cached schema
        if (_state.JsonLinesSchemaScanner is not null && _state.Schema is not null)
        {
            _state.CurrentMode = ViewMode.JsonLinesTable;
            return;
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
        }
        catch (InvalidOperationException ex)
        {
            _state.LastError = ex.Message;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _state.Cts.Cancel();
    }
}
