using AwesomeAssertions;
using DataMorph.App;
using DataMorph.App.Views;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App;

public sealed class ViewManagerTests : IDisposable
{
    private readonly List<string> _tempFiles = [];
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            _disposed = true;
        }
    }

    private string CreateTempFile(string extension, string content)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        File.WriteAllText(filePath, content);
        _tempFiles.Add(filePath);
        return filePath;
    }

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
        var filePath = CreateTempFile(".csv", "col1,col2\nvalue1,value2\n");
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

    [Fact]
    public void RefreshStatusBarHints_WithJsonLinesPath_IncludesToggleHint()
    {
        // Arrange
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
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

    [Fact]
    public void RefreshStatusBarHints_WithJsonArrayFilePath_IncludesToggleHint()
    {
        // Arrange
        var filePath = CreateTempFile(".json", "[1,2,3]");
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

    [Fact]
    public void RefreshStatusBarHints_WithMorphTableView_IncludesMenuHint()
    {
        // Arrange
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
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
        viewManager.SwitchToCsvTable(new MockRowIndexer(filePath), schema);

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

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenToggleFails_ShowsError()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState
        {
            CurrentFilePath = string.Empty,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new MockRowIndexer("test.jsonl")
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
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
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
        state.RowIndexer = new MockRowIndexer(filePath);

        // Act
        await viewManager.ToggleJsonLinesModeAsync();

        // Assert
        // After toggle, mode should become JsonLinesTree
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenModeBecomesTable_SwitchesToTableView()
    {
        // Arrange
        var filePath = CreateTempFile(".jsonl", "{\"col1\": \"value\"}\n");
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
        state.RowIndexer = new MockRowIndexer(filePath);

        // Act
        await viewManager.ToggleJsonLinesModeAsync();

        // Assert
        // After toggle, mode should become JsonLinesTable
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
    }

    [Fact]
    public async Task ToggleJsonArrayModeAsync_WhenCalled_ReturnsCompletedTask()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());

        // Act
        var task = viewManager.ToggleJsonArrayModeAsync();

        // Assert
        await task;
        task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleJsonArrayModeAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        var viewManager = new ViewManager(window, state, modeController, action => action());
        viewManager.Dispose();

        // Act
        var act = () => viewManager.ToggleJsonArrayModeAsync();

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void SwitchToJsonArrayTree_WithValidIndexer_SetsCurrentView()
    {
        // Arrange
        var filePath = CreateTempFile(".json", "[1,2,3]");
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = filePath };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        viewManager.SwitchToJsonArrayTree(indexer);

        // Assert
        viewManager.GetCurrentView().Should().BeOfType<JsonArrayTreeView>();
    }

    [Fact]
    public void SwitchToJsonArrayTree_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController, action => action());
        IRowIndexer? nullIndexer = null;

        // Act
        var act = () => viewManager.SwitchToJsonArrayTree(nullIndexer!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SwitchToJsonArrayTree_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        var modeController = new ModeController(state);
        var viewManager = new ViewManager(window, state, modeController, action => action());
        viewManager.Dispose();

        // Act
        var act = () => viewManager.SwitchToJsonArrayTree(new MockRowIndexer("test.json"));

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    /// <summary>
    /// Mock IRowIndexer for testing.
    /// </summary>
    private sealed class MockRowIndexer(string filePath) : IRowIndexer
    {
        public string FilePath => filePath;
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
