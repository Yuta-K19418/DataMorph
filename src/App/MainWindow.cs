using System.Diagnostics.CodeAnalysis;
using DataMorph.Engine.Models;
using DataMorph.Engine.Recipes;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App;

/// <summary>
/// Main application window for DataMorph TUI.
/// Owns the menu and status bar; delegates file loading to <see cref="FileLoader"/>
/// and content view management to <see cref="ViewManager"/>.
/// </summary>
internal sealed class MainWindow : Window
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private readonly FileLoader _fileLoader;
    private readonly ViewManager _viewManager;
    private readonly RecipeManager _recipeManager = new();

    public MainWindow(IApplication app, AppState state)
    {
        _app = app;
        _state = state;
        _fileLoader = new FileLoader(state);

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
            _fileLoader.Dispose();
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

        var result = await _fileLoader.LoadAsync(dialog.Path);

        if (result.IsFailure)
        {
            _viewManager.ShowError(result.Error);
            return;
        }

        if (
            _state.CurrentMode == ViewMode.CsvTable
            && _state.CsvIndexer is not null
            && _state.Schema is not null
        )
        {
            _viewManager.SwitchToCsvTable(_state.CsvIndexer, _state.Schema);
            return;
        }

        if (_state.CurrentMode == ViewMode.JsonLinesTree && _state.JsonLinesIndexer is not null)
        {
            _viewManager.SwitchToJsonLinesTree(_state.JsonLinesIndexer);
        }
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
        var result = await _fileLoader.ToggleJsonLinesModeAsync();

        if (result.IsFailure)
        {
            _viewManager.ShowError(result.Error);
            return;
        }

        if (_state.CurrentMode == ViewMode.JsonLinesTree && _state.JsonLinesIndexer is not null)
        {
            _viewManager.SwitchToJsonLinesTree(_state.JsonLinesIndexer);
            return;
        }

        if (
            _state.CurrentMode == ViewMode.JsonLinesTable
            && _state.JsonLinesIndexer is not null
            && _state.Schema is not null
        )
        {
            _viewManager.SwitchToJsonLinesTableView(_state.JsonLinesIndexer, _state.Schema);
        }
    }
}
