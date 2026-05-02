using AwesomeAssertions;
using DataMorph.App;
using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App;

public sealed class ViewManagerTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public void RefreshStatusBarHints_WithNoFilePath_UsesDefaultHints()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = string.Empty };
        using var window = new Window();
        using var statusBar = new StatusBar();
        window.Add(statusBar);
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        var currentStatusBar = viewManager.GetCurrentStatusBar();
        currentStatusBar.Should().NotBeNull();
        var hints = Enumerable.Select(
                Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                s => s.HelpText);
        hints.Should().BeEquivalentTo(
            ["o:Open", "s:Save", "q:Quit", "?:Help"],
            opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void RefreshStatusBarHints_WithCsvFilePath_UsesDefaultHints()
    {
        // Arrange
        const string filePath = "test.csv";
        File.WriteAllText(filePath, "col1,col2\nvalue1,value2\n");
        try
        {
            using var app = CreateTestApp();
            using var state = new AppState { CurrentFilePath = filePath };
            using var window = new Window();
            using var statusBar = new StatusBar();
            window.Add(statusBar);
            var modeController = new ModeController(state);
            using var viewManager = new ViewManager(window, state, modeController, action => action());

            // Act
            viewManager.RefreshStatusBarHints();

            // Assert
            var currentStatusBar = viewManager.GetCurrentStatusBar();
            currentStatusBar.Should().NotBeNull();
            var hints = Enumerable.Select(
                    Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                    s => s.HelpText);
            hints.Should().BeEquivalentTo(
                ["o:Open", "s:Save", "q:Quit", "?:Help"],
                opts => opts.WithStrictOrdering());
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void RefreshStatusBarHints_WithJsonLinesPath_IncludesToggleHint()
    {
        // Arrange
        const string filePath = "test.jsonl";
        File.WriteAllText(filePath, "{\"col1\": \"value\"}\n");
        try
        {
            using var app = CreateTestApp();
            using var state = new AppState { CurrentFilePath = filePath };
            using var window = new Window();
            using var statusBar = new StatusBar();
            window.Add(statusBar);
            var modeController = new ModeController(state);
            using var viewManager = new ViewManager(window, state, modeController, action => action());

            // Act
            viewManager.RefreshStatusBarHints();

            // Assert
            var currentStatusBar = viewManager.GetCurrentStatusBar();
            currentStatusBar.Should().NotBeNull();
            var hints = Enumerable.Select(
                    Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                    s => s.HelpText);
            hints.Should().Contain("t:Tree/Table");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void RefreshStatusBarHints_WithMorphTableView_IncludesMenuHint()
    {
        // Arrange
        const string filePath = "test.jsonl";
        File.WriteAllText(filePath, "{\"col1\": \"value\"}\n");
        try
        {
            using var app = CreateTestApp();
            using var state = new AppState { CurrentFilePath = filePath };
            using var window = new Window();
            using var statusBar = new StatusBar();
            window.Add(statusBar);
            var modeController = new ModeController(state);
            using var viewManager = new ViewManager(window, state, modeController, action => action());

            var schema = new TableSchema
            {
                SourceFormat = DataFormat.JsonLines,
                Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
            };
            viewManager.SwitchToCsvTable(new MockRowIndexer(), schema);

            // Act
            viewManager.RefreshStatusBarHints();

            // Assert
            var currentStatusBar = viewManager.GetCurrentStatusBar();
            currentStatusBar.Should().NotBeNull();
            var hints = Enumerable.Select(
                    Enumerable.OfType<Shortcut>(currentStatusBar.SubViews),
                    s => s.HelpText);
            hints.Should().Contain("x:Menu");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenToggleFails_ShowsError()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentFilePath = string.Empty,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new MockRowIndexer()
        };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        await viewManager.ToggleJsonLinesModeAsync();

        // Assert
        state.CurrentMode.Should().Be(ViewMode.PlaceholderView);
        viewManager.GetCurrentView()?.Text.Should().Be("No file is currently open");
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenModeBecomesTree_SwitchesToTreeView()
    {
        // Arrange
        const string filePath = "test.jsonl";
        File.WriteAllText(filePath, "{\"col1\": \"value\"}\n");
        try
        {
            using var app = CreateTestApp();
            using var state = new AppState { CurrentFilePath = filePath, CurrentMode = ViewMode.JsonLinesTable };
            using var window = new Window();
            var modeController = new ModeController(state);
            using var viewManager = new ViewManager(window, state, modeController, action => action());

            // Setup a valid table state
            var schema = new TableSchema
            {
                SourceFormat = DataFormat.JsonLines,
                Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
            };
            state.Schema = schema;
            state.RowIndexer = new MockRowIndexer();

            // Act
            await viewManager.ToggleJsonLinesModeAsync();

            // Assert
            // After toggle, mode should become JsonLinesTree
            state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenModeBecomesTable_SwitchesToTableView()
    {
        // Arrange
        const string filePath = "test.jsonl";
        File.WriteAllText(filePath, "{\"col1\": \"value\"}\n");
        try
        {
            using var app = CreateTestApp();
            using var state = new AppState { CurrentFilePath = filePath, CurrentMode = ViewMode.JsonLinesTree };
            using var window = new Window();
            var modeController = new ModeController(state);
            using var viewManager = new ViewManager(window, state, modeController, action => action());

            // Setup a valid tree state
            var schema = new TableSchema
            {
                SourceFormat = DataFormat.JsonLines,
                Columns = [new ColumnSchema { Name = "col1", Type = ColumnType.Text }]
            };
            state.Schema = schema;
            state.RowIndexer = new MockRowIndexer();

            // Act
            await viewManager.ToggleJsonLinesModeAsync();

            // Assert
            // After toggle, mode should become JsonLinesTable
            state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// Mock IRowIndexer for testing.
    /// </summary>
    private sealed class MockRowIndexer : IRowIndexer
    {
        public string FilePath => "test.jsonl";
        public long FileSize => 1000;
        public long BytesRead => 1000;
        public long TotalRows => 10;

#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;
#pragma warning restore CS0067

        public void BuildIndex(CancellationToken cancellationToken = default) { }

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => (0, 0);
    }
}
