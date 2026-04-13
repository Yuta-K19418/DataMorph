using System.Diagnostics.CodeAnalysis;
using DataMorph.Engine.IO;
using Terminal.Gui.App;
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
    private readonly AppKeyHandler _keyHandler;
    private readonly FileDialogHandler _fileDialogHandler;
    private readonly RecipeCommandHandler _recipeCommandHandler;
    private IRowIndexer? _activeIndexer;

    private Action<long, long>? _onProgressChanged;
    private Action? _onBuildIndexCompleted;

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

    [SuppressMessage(
        "Reliability",
        "CA2213:Disposable fields should be disposed",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private StatusBar? _statusBar;

    public MainWindow(IApplication app, AppState state)
    {
        _app = app;
        _state = state;
        _modeController = new ModeController(state);

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        _viewManager = new ViewManager(this, state, _modeController);

        _fileDialogHandler = new FileDialogHandler(app, state, _viewManager, StartIndexing);
        _recipeCommandHandler = new RecipeCommandHandler(app, state, _viewManager);

        InitializeMenu();
        InitializeStatusBar();
        _keyHandler = new AppKeyHandler(app, state, _viewManager, _fileDialogHandler, _recipeCommandHandler, _statusBar);
        _viewManager.SwitchToFileSelection();
    }

    /// <summary>
    /// Subscribes the global key handler to the application keyboard events.
    /// Should be called after Application.Init().
    /// </summary>
    internal void SubscribeKeyHandler()
    {
        _keyHandler.Subscribe();
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private void InitializeMenu()
    {
        var openMenuItem = new MenuItem("_Open", "", async () => await _fileDialogHandler.ShowAsync());
        var saveRecipeMenuItem = new MenuItem("_Save Recipe", "", async () => await _recipeCommandHandler.SaveAsync());
        var loadRecipeMenuItem = new MenuItem("_Load Recipe", "", async () => await _recipeCommandHandler.LoadAsync());
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
        _statusBar = new StatusBar
        {
            X = 0,
            Y = Pos.Bottom(this),
            Width = Dim.Fill(),
            Height = 1,
        };

        _viewManager.RefreshStatusBarHints();
        Add(_statusBar);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _keyHandler.Dispose();
            _indexTaskManager.Dispose();
            _state.Dispose();
            _viewManager.Dispose();
        }
        base.Dispose(disposing);
    }

    private void WireIndexerProgress(IRowIndexer indexer)
    {
        // Unsubscribe from the previous indexer to prevent event handler leaks
        // when a new file is opened while a previous indexer is still active.
        if (_activeIndexer is not null && _onProgressChanged is not null && _onBuildIndexCompleted is not null)
        {
            _activeIndexer.ProgressChanged -= _onProgressChanged;
            _activeIndexer.BuildIndexCompleted -= _onBuildIndexCompleted;
        }

        ShowIndexingProgress();

        _onProgressChanged = OnProgressChanged;
        _onBuildIndexCompleted = OnBuildIndexCompleted;
        _activeIndexer = indexer;
        indexer.ProgressChanged += _onProgressChanged;
        indexer.BuildIndexCompleted += _onBuildIndexCompleted;

        UpdateIndexingProgress(indexer.BytesRead, indexer.FileSize);
    }

    private void OnProgressChanged(long bytesRead, long fileSize)
    {
        _app.Invoke(() => UpdateIndexingProgress(bytesRead, fileSize));
    }

    private void OnBuildIndexCompleted()
    {
        _app.Invoke(DismissIndexingProgress);
    }

    internal void StartIndexing(IRowIndexer indexer)
    {
        WireIndexerProgress(indexer);
        _indexTaskManager.Start(indexer);
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
