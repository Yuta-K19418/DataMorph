using System.Diagnostics.CodeAnalysis;
using DataMorph.App.Schema;
using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App;

/// <summary>
/// Main application window for DataMorph TUI.
/// Manages content view switching and file operations.
/// </summary>
internal sealed class MainWindow : Window
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private View? _currentContentView;

    public MainWindow(IApplication app, AppState state)
    {
        _app = app;
        _state = state;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        InitializeMenu();
        InitializeStatusBar();
        InitializeContentView();
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private void InitializeMenu()
    {
        var openMenuItem = new MenuItem("_Open", "", async () => await ShowFileDialogAsync());
        var exitMenuItem = new MenuItem("_Exit", "", () => _app.RequestStop());
        var fileMenuBarItem = new MenuBarItem("_File", [openMenuItem, exitMenuItem]);
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
        var quitShortcut = new Shortcut
        {
            Key = KeyCode.X | KeyCode.CtrlMask,
            Title = "Quit",
            Action = () => _app.RequestStop(),
        };
        var statusBar = new StatusBar([openShortcut, quitShortcut]);

        Add(statusBar);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _currentContentView is not null)
        {
            Remove(_currentContentView);
            _currentContentView.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeContentView()
    {
        _currentContentView = Views.FileSelectionView.Create();
        Add(_currentContentView);
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

        _state.CurrentFilePath = dialog.Path;

        // Determine file type and create appropriate view
        if (dialog.Path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            await LoadCsvFileAsync(dialog.Path);
            return;
        }

        if (dialog.Path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
        {
            await LoadJsonLinesFileAsync(dialog.Path);
            return;
        }

        // JSON files: use placeholder view for now
        _state.CurrentMode = ViewMode.PlaceholderView;

        if (_currentContentView is not null)
        {
            Remove(_currentContentView);
            _currentContentView.Dispose();
        }
        _currentContentView = Views.PlaceholderView.Create(_state);
        Add(_currentContentView);
    }

    private async Task LoadCsvFileAsync(string filePath)
    {
        var indexer = new CsvDataRowIndexer(filePath);
        _ = Task.Run(indexer.BuildIndex);

        var schemaScanner = new IncrementalSchemaScanner(filePath);

        try
        {
            var schema = await schemaScanner.InitialScanAsync();
            _state.Schema = schema;
            _state.SchemaScanner = schemaScanner;

            _ = Task.Run(async () =>
            {
                var refinedSchema = await schemaScanner.StartBackgroundScanAsync(
                    schema,
                    _state.Cts.Token
                );
                _state.Schema = refinedSchema;
            });
            SwitchToTableView(indexer, schema);
        }
        catch (ArgumentException ex)
        {
            ShowError(ex.Message);
            return;
        }
        catch (IOException ex)
        {
            ShowError($"Error reading CSV file: {ex.Message}");
            return;
        }

        return;
    }

    private void ShowError(string error)
    {
        _state.CurrentMode = ViewMode.PlaceholderView;

        if (_currentContentView is not null)
        {
            Remove(_currentContentView);
            _currentContentView.Dispose();
        }
        _currentContentView = Views.PlaceholderView.Create(_state);
        _currentContentView.Text = error;
        Add(_currentContentView);
    }

    private Task LoadJsonLinesFileAsync(string filePath)
    {
        var indexer = new Engine.IO.JsonLines.RowIndexer(filePath);
        _ = Task.Run(indexer.BuildIndex);

        SwitchToTreeView(indexer);

        return Task.CompletedTask;
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private void SwitchToTreeView(Engine.IO.JsonLines.RowIndexer indexer)
    {
        _state.CurrentMode = ViewMode.JsonLinesTree;

        if (_currentContentView is not null)
        {
            Remove(_currentContentView);
            _currentContentView.Dispose();
        }

        _currentContentView = new Views.JsonLinesTreeView(indexer)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        Add(_currentContentView);
    }

    private void SwitchToTableView(CsvDataRowIndexer indexer, TableSchema schema)
    {
        _state.CurrentMode = ViewMode.CsvTable;

        if (_currentContentView is not null)
        {
            Remove(_currentContentView);
            _currentContentView.Dispose();
        }

        _currentContentView = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = new Views.VirtualTableSource(indexer, schema),
            Style = new TableStyle() { AlwaysShowHeaders = true },
        };
        Add(_currentContentView);
    }
}
