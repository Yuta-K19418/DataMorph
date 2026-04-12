using System.Diagnostics.CodeAnalysis;
using DataMorph.App.Schema.Csv;
using DataMorph.App.Views;
using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using DataMorph.Engine.Recipes;
using DataMorph.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace DataMorph.App;

/// <summary>
/// Handles file operations for DataMorph application.
/// </summary>
internal sealed class FileOperationsService
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private readonly ViewManager _viewManager;
    private readonly RecipeManager _recipeManager = new();
    private readonly ModeController _modeController;

    internal FileOperationsService(
        IApplication app,
        AppState state,
        ViewManager viewManager,
        ModeController modeController
    )
    {
        _app = app;
        _state = state;
        _viewManager = viewManager;
        _modeController = modeController;
    }

    /// <summary>
    /// Updates status bar hints based on current state.
    /// </summary>
    internal void UpdateStatusBarHints(StatusBar? statusBar)
    {
        if (statusBar is null)
        {
            return;
        }

        List<string> hints = ["o:Open", "s:Save", "q:Quit"];

        if (_state.CurrentFilePath is not null)
        {
            var format = FormatDetector.Detect(_state.CurrentFilePath);
            if (format.IsSuccess && format.Value == DataFormat.JsonLines)
            {
                hints.Add("t:Tree/Table");
            }

            if (_viewManager.GetCurrentView() is MorphTableView)
            {
                hints.Add("x:Menu");
            }
        }

        hints.Add("?:Help");

        statusBar.Text = string.Join("  ", hints);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically."
    )]
    internal async Task ShowFileDialogAsync(Action<IRowIndexer> onIndexerStart)
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

                onIndexerStart(indexer);
                UpdateStatusBarHints(_viewManager.GetCurrentStatusBar());
                return;
            }
#pragma warning disable CA1031 // UI top-level handler
            catch (Exception ex)
#pragma warning restore CA1031 // UI top-level handler
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

        onIndexerStart(indexer);
        UpdateStatusBarHints(_viewManager.GetCurrentStatusBar());
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically."
    )]
    internal async Task HandleSaveRecipeAsync()
    {
        if (_state.CurrentMode is not (ViewMode.CsvTable or ViewMode.JsonLinesTable or ViewMode.JsonLinesTree))
        {
            return;
        }

        if (string.IsNullOrEmpty(_state.CurrentFilePath))
        {
            return;
        }

        var dialog = new OpenDialog { Title = "Save Recipe" };
        dialog.AllowedTypes.Add(new AllowedType("YAML file", ".yaml"));

        _app.Run(dialog);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        var recipe = new Recipe
        {
            Name = Path.GetFileNameWithoutExtension(_state.CurrentFilePath),
            Actions = _state.ActionStack,
            LastModified = DateTimeOffset.UtcNow,
        };

        var result = await _recipeManager.SaveAsync(recipe, dialog.Path);

        _app.Invoke(() =>
        {
            if (result.IsFailure)
            {
                _viewManager.ShowError(result.Error);
                return;
            }

            MessageBox.Query(_app, "Save Recipe", "Recipe saved successfully.", "OK");
        });
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically."
    )]
    internal async Task HandleLoadRecipeAsync()
    {
        if (string.IsNullOrEmpty(_state.CurrentFilePath))
        {
            return;
        }

        var dialog = new OpenDialog { Title = "Load Recipe" };
        dialog.AllowedTypes.Add(new AllowedType("YAML file", ".yaml"));

        _app.Run(dialog);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        var result = await _recipeManager.LoadAsync(dialog.Path);

        _app.Invoke(() =>
        {
            if (result.IsFailure)
            {
                _viewManager.ShowError(result.Error);
                return;
            }

            _state.ActionStack = result.Value.Actions;
            _viewManager.RefreshCurrentTableView();
            UpdateStatusBarHints(_viewManager.GetCurrentStatusBar());
        });
    }

    internal async Task HandleToggleAsync()
    {
        var result = await _modeController.ToggleJsonLinesModeAsync();

        if (result.IsFailure)
        {
            _viewManager.ShowError(result.Error);
            UpdateStatusBarHints(_viewManager.GetCurrentStatusBar());
            return;
        }

        if (_state.CurrentMode == ViewMode.JsonLinesTree && _state.RowIndexer is not null)
        {
            _viewManager.SwitchToJsonLinesTree(_state.RowIndexer);
            UpdateStatusBarHints(_viewManager.GetCurrentStatusBar());
            return;
        }

        if (
            _state.CurrentMode == ViewMode.JsonLinesTable
            && _state.RowIndexer is not null
            && _state.Schema is not null
        )
        {
            _viewManager.SwitchToJsonLinesTableView(_state.RowIndexer, _state.Schema);
            UpdateStatusBarHints(_viewManager.GetCurrentStatusBar());
        }
    }
}
