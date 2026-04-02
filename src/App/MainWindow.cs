using System.Diagnostics.CodeAnalysis;
using DataMorph.App.Schema.Csv;
using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using DataMorph.Engine.Recipes;
using DataMorph.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App;

/// <summary>
/// Main application window for DataMorph TUI.
/// Owns the menu and status bar; orchestrates file loading
/// and content view management via <see cref="ViewManager"/>.
/// </summary>
internal sealed class MainWindow : Window
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private readonly IndexTaskManager _indexTaskManager = new();
    private readonly ModeController _modeController;
    private readonly ViewManager _viewManager;
    private readonly RecipeManager _recipeManager = new();

    private Action? _onBuildIndexCompleted;
    private Action<long, long>? _onProgressChanged;

    [SuppressMessage(
        "Reliability",
        "CA2213:Disposable fields should be disposed",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private ProgressBar? _progressBar;

    [SuppressMessage(
        "Reliability",
        "CA2213:Disposable fields should be disposed",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private Label? _progressLabel;

    public MainWindow(IApplication app, AppState state)
    {
        _app = app;
        _state = state;
        _modeController = new ModeController(state);

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _viewManager = new ViewManager(this, state, HandleToggleAsync);

        InitializeMenu();
        InitializeStatusBar();
        _viewManager.SwitchToFileSelection();
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private void InitializeMenu()
    {
        var openMenuItem = new MenuItem("_Open", "", async () => await ShowFileDialogAsync());
        var saveRecipeMenuItem = new MenuItem("_Save Recipe", "", async () => await HandleSaveRecipeAsync());
        var loadRecipeMenuItem = new MenuItem("_Load Recipe", "", async () => await HandleLoadRecipeAsync());
        var exitMenuItem = new MenuItem("_Exit", "", () => _app.RequestStop());
        var fileMenuBarItem = new MenuBarItem("_File", [openMenuItem, saveRecipeMenuItem, loadRecipeMenuItem, exitMenuItem]);
        var menuBar = new MenuBar { Menus = [fileMenuBarItem] };

        Add(menuBar);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private void InitializeStatusBar()
    {
        var openShortcut = new Shortcut
        {
            Key = KeyCode.O | KeyCode.CtrlMask,
            Title = "Open",
            Action = async () => await ShowFileDialogAsync(),
        };
        var saveRecipeShortcut = new Shortcut
        {
            Key = KeyCode.S | KeyCode.CtrlMask,
            Title = "Save Recipe",
            Action = async () => await HandleSaveRecipeAsync(),
        };
        var quitShortcut = new Shortcut
        {
            Key = KeyCode.X | KeyCode.CtrlMask,
            Title = "Quit",
            Action = () => _app.RequestStop(),
        };
        var statusBar = new StatusBar([openShortcut, saveRecipeShortcut, quitShortcut]);

        Add(statusBar);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _indexTaskManager.Dispose();
            _state.Dispose();
            _viewManager.Dispose();
        }
        base.Dispose(disposing);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically."
    )]
    private async Task ShowFileDialogAsync()
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

        // Wire events on new indexer (before Start)
        WireIndexerProgress(indexer);

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
            }
#pragma warning disable CA1031 // UI top-level handler
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _viewManager.ShowError($"Error scanning CSV: {ex.Message}");
                return;
            }
        }
        else if (format == DataFormat.JsonLines)
        {
            _state.RowIndexer = indexer;
            _state.JsonLinesSchemaScanner = null;
            _state.Schema = null;
            _state.OnSchemaRefined = null;
            _state.CurrentMode = ViewMode.JsonLinesTree;

            _viewManager.SwitchToJsonLinesTree(indexer);
        }

        // Start the background indexing task
        _indexTaskManager.Start(indexer);
    }

    private void WireIndexerProgress(IRowIndexer indexer)
    {
        // Unsubscribe old handlers from previous indexer if they exist
        if (_state.RowIndexer is not null)
        {
            if (_onProgressChanged is not null)
            {
                _state.RowIndexer.ProgressChanged -= _onProgressChanged;
            }

            if (_onBuildIndexCompleted is not null)
            {
                _state.RowIndexer.BuildIndexCompleted -= _onBuildIndexCompleted;
            }
        }

        ShowIndexingProgress();

        _onProgressChanged = (bytesRead, fileSize) =>
            _app.Invoke(() => UpdateIndexingProgress(bytesRead, fileSize));

        _onBuildIndexCompleted = () => _app.Invoke(DismissIndexingProgress);

        indexer.ProgressChanged += _onProgressChanged;
        indexer.BuildIndexCompleted += _onBuildIndexCompleted;

        UpdateIndexingProgress(indexer.BytesRead, indexer.FileSize);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically."
    )]
    private async Task HandleSaveRecipeAsync()
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
    private async Task HandleLoadRecipeAsync()
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
        });
    }

    private async Task HandleToggleAsync()
    {
        var result = await _modeController.ToggleJsonLinesModeAsync();

        if (result.IsFailure)
        {
            _viewManager.ShowError(result.Error);
            return;
        }

        if (_state.CurrentMode == ViewMode.JsonLinesTree && _state.RowIndexer is not null)
        {
            _viewManager.SwitchToJsonLinesTree(_state.RowIndexer);
            return;
        }

        if (
            _state.CurrentMode == ViewMode.JsonLinesTable
            && _state.RowIndexer is not null
            && _state.Schema is not null
        )
        {
            _viewManager.SwitchToJsonLinesTableView(_state.RowIndexer, _state.Schema);
        }
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private void ShowIndexingProgress()
    {
        DismissIndexingProgress();
        _progressBar = new ProgressBar
        {
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = Dim.Percent(60),
        };
        _progressLabel = new Label
        {
            X = Pos.Center(),
            Y = Pos.Bottom(_progressBar) + 1,
            Text = "Indexing…",
        };

        Add(_progressBar, _progressLabel);
    }

    private void UpdateIndexingProgress(long bytesRead, long fileSize)
    {
        if (_progressBar is null || _progressLabel is null)
        {
            return;
        }

        if (fileSize <= 0)
        {
            return;
        }

        var fraction = (float)bytesRead / fileSize;
        _progressBar.Fraction = fraction;
        _progressLabel.Text =
            $"Indexing… {fraction * 100:F0}%  " +
            $"({FormatBytes(bytesRead)} / {FormatBytes(fileSize)})";
    }

    private void DismissIndexingProgress()
    {
        if (_progressBar is not null)
        {
            Remove(_progressBar);
            _progressBar.Dispose();
            _progressBar = null;
        }

        if (_progressLabel is not null)
        {
            Remove(_progressLabel);
            _progressLabel.Dispose();
            _progressLabel = null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;

        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F2} GB",
            >= MB => $"{bytes / (double)MB:F2} MB",
            >= KB => $"{bytes / (double)KB:F2} KB",
            _ => $"{bytes} B",
        };
    }
}
