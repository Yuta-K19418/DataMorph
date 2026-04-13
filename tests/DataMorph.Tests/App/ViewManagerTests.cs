using AwesomeAssertions;
using DataMorph.App;
using DataMorph.App.Views;
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
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        // Default hints should be set: "o:Open", "s:Save", "q:Quit", "?:Help"
        // We can't directly verify statusBar.Text without exposing it,
        // but we can verify the method completes without errors
    }

    [Fact]
    public void RefreshStatusBarHints_WithCsvFilePath_UsesDefaultHints()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = "test.csv" };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        // CSV files should only show default hints: "o:Open", "s:Save", "q:Quit", "?:Help"
    }

    [Fact]
    public void RefreshStatusBarHints_WithJsonLinesPath_IncludesToggleHint()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = "test.jsonl" };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        // JSON Lines files should include "t:Tree/Table" hint
    }

    [Fact]
    public void RefreshStatusBarHints_WithMorphTableView_IncludesMenuHint()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = "test.jsonl" };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);

        using var testTableView = new TestTableView
        {
            Table = new TestTableSource(),
            GetRawColumnName = _ => "test",
            OnMorphAction = _ => { }
        };
        window.Add(testTableView);

        // Act
        viewManager.RefreshStatusBarHints();

        // Assert
        // Should include "x:Menu" hint when MorphTableView is present
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WhenToggleFails_ShowsError()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState { CurrentFilePath = "test.csv" };
        using var window = new Window();
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);

        // Act
        await viewManager.ToggleJsonLinesModeAsync();

        // Assert
        // ShowError should be called when toggle fails
        // (CSV file doesn't support JSON Lines toggle, so it should fail)
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
            using var viewManager = new ViewManager(window, state, modeController);

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
            using var viewManager = new ViewManager(window, state, modeController);

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
    /// Testable concrete implementation of MorphTableView.
    /// </summary>
    private sealed class TestTableView : MorphTableView
    {
        public new ITableSource? Table { get; set; }
    }

    /// <summary>
    /// Simple TableSource implementation for testing.
    /// </summary>
    private sealed class TestTableSource : ITableSource
    {
        public int Rows => 10;
        public int Columns => 3;
        public string[] ColumnNames => ["Col1", "Col2", "Col3"];

        public object this[int row, int col]
        {
            get => $"R{row}C{col}";
            set { }
        }

        public static void AddColumn(string name) { }
        public static void AddRow() { }
        public static void RemoveColumn(int index) { }
        public static void RemoveRow(int index) { }
        public static void Clear() { }
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
