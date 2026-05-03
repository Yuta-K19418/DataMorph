using System.Diagnostics.CodeAnalysis;
using DataMorph.App.Schema.Csv;
using DataMorph.Engine.IO;
using DataMorph.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace DataMorph.App;

/// <summary>
/// Handles file dialog operations for opening data files.
/// </summary>
internal sealed class FileDialogHandler(
    IApplication app,
    AppState state,
    ViewManager viewManager,
    Action<IRowIndexer> onIndexerStart)
{
    private readonly IApplication _app = app;
    private readonly AppState _state = state;
    private readonly ViewManager _viewManager = viewManager;
    private readonly Action<IRowIndexer> _onIndexerStart = onIndexerStart;

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically."
    )]
    internal async Task ShowAsync()
    {
        var dialog = new OpenDialog { Title = "Open File" };
        dialog.AllowedTypes.Add(new AllowedType("CSV file", ".csv"));
        dialog.AllowedTypes.Add(new AllowedType("JSON file", ".json"));
        dialog.AllowedTypes.Add(new AllowedType("JSON Lines file", ".jsonl"));

        _app.Run(dialog);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        await HandleFileSelectedAsync(dialog.Path);
    }

    internal async Task HandleFileSelectedAsync(string path)
    {
        var detectionResult = FormatDetector.Detect(path);
        if (detectionResult.IsFailure)
        {
            _viewManager.ShowError(detectionResult.Error);
            return;
        }

        var format = detectionResult.Value;

        // Reset state for new file
        _state.CurrentFilePath = path;
        _state.ActionStack = [];
        _state.RenewCtsWithCancel();

        // Create indexer from factory
        var indexer = RowIndexerFactory.Create(format, path);

        // SwitchToView(format)
        if (format == DataFormat.Csv)
        {
            var schemaScanner = new IncrementalSchemaScanner(path);
            try
            {
                var schema = await schemaScanner.InitialScanAsync();
                _app.Invoke(() =>
                {
                    if (schema.Columns.Count == 0)
                    {
                        _viewManager.ShowError("File contains no data");
                        return;
                    }

                    _state.Schema = schema;
                    _state.RowIndexer = indexer;
                    _state.CurrentMode = ViewMode.CsvTable;

                    _viewManager.SwitchToCsvTable(indexer, schema);

                    _ = schemaScanner
                        .StartBackgroundScanAsync(schema, _state.Cts.Token)
                        .ContinueWith(
                            t =>
                            {
                                if (!t.IsCompletedSuccessfully)
                                {
                                    return;
                                }

                                _app.Invoke(() =>
                                {
                                    _state.Schema = t.Result;
                                    _state.OnSchemaRefined?.Invoke(t.Result);
                                });
                            },
                            TaskScheduler.Default
                        );

                    _onIndexerStart(indexer);
                });
                return;
            }
#pragma warning disable CA1031 // UI top-level handler
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _app.Invoke(() => _viewManager.ShowError($"Error scanning CSV: {ex.Message}"));
                return;
            }
        }

        if (format == DataFormat.JsonLines)
        {
            try
            {
                _state.RowIndexer = indexer;
                _state.Schema = null;
                _state.OnSchemaRefined = null;

                var tcs = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                indexer.FirstCheckpointReached += () => tcs.TrySetResult();

                _onIndexerStart(indexer);
                await tcs.Task;

                _app.Invoke(() =>
                {
                    _state.CurrentMode = ViewMode.JsonLinesTree;
                    _viewManager.SwitchToJsonLinesTree(indexer);
                });
                return;
            }
#pragma warning disable CA1031 // UI top-level handler
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _app.Invoke(() => _viewManager.ShowError($"Error loading JSON Lines: {ex.Message}"));
                return;
            }
        }

        _onIndexerStart(indexer);
    }
}
