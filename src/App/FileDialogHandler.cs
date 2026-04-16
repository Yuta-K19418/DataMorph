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

        var detectionResult = FormatDetector.Detect(dialog.Path);
        if (detectionResult.IsFailure)
        {
            _viewManager.ShowError(detectionResult.Error);
            return;
        }

        var format = detectionResult.Value;
        var path = dialog.Path;

        // Reset state for new file
        _state.CurrentFilePath = path;
        _state.ActionStack = [];

        // Create indexer from factory
        var indexer = RowIndexerFactory.Create(format, path);

        // SwitchToView(format)
        if (format == DataFormat.Csv)
        {
            var schemaScanner = new IncrementalSchemaScanner(path);
            try
            {
                var schema = await schemaScanner.InitialScanAsync();
                if (schema.Columns.Count == 0)
                {
                    _viewManager.ShowError("File contains no data");
                    return;
                }

                _state.Schema = schema;
                _state.RowIndexer = indexer;
                _state.CsvSchemaScanner = schemaScanner;
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

                            _state.Schema = t.Result;
                            _state.OnSchemaRefined?.Invoke(t.Result);
                        },
                        TaskScheduler.Default
                    );

                _onIndexerStart(indexer);
                return;
            }
#pragma warning disable CA1031 // UI top-level handler
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _viewManager.ShowError($"Error scanning CSV: {ex.Message}");
                return;
            }
        }

        if (format == DataFormat.JsonLines)
        {
            _state.RowIndexer = indexer;
            _state.JsonLinesSchemaScanner = null;
            _state.Schema = null;
            _state.OnSchemaRefined = null;
            _state.CurrentMode = ViewMode.JsonLinesTree;

            _viewManager.SwitchToJsonLinesTree(indexer);
        }

        _onIndexerStart(indexer);
    }
}
