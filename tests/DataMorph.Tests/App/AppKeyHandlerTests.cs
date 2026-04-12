using AwesomeAssertions;
using DataMorph.App;
using DataMorph.App.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App;

public sealed class AppKeyHandlerTests
{
    [Theory]
    [InlineData((KeyCode)'o')]
    [InlineData((KeyCode)'s')]
    [InlineData((KeyCode)'q')]
    [InlineData((KeyCode)'t')]
    [InlineData((KeyCode)'x')]
    [InlineData((KeyCode)'?')]
    public void IsGlobalShortcut_WithGlobalShortcutKeys_ReturnsTrue(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData((KeyCode)'a')]
    [InlineData((KeyCode)'b')]
    [InlineData((KeyCode)'z')]
    [InlineData((KeyCode)'1')]
    public void IsGlobalShortcut_WithNonGlobalShortcutKeys_ReturnsFalse(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData((KeyCode)'o' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'s' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'q' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'t' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'x' | KeyCode.CtrlMask)]
    [InlineData((KeyCode)'?' | KeyCode.CtrlMask)]
    public void IsGlobalShortcut_WithModifierKeys_ReturnsTrue(KeyCode keyCode)
    {
        // Arrange
        // Act
        var result = AppKeyHandler.IsGlobalShortcut(keyCode);

        // Assert
        // Modifier keys are ignored - only the base character is checked
        result.Should().BeTrue();
    }

    [Fact]
    public void HandleActionMenu_WhenCurrentViewIsNotMorphTableView_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        var modeController = new ModeController(state);
        var fileOperations = new FileOperationsService(app, state, viewManager, modeController);
        using var handler = new AppKeyHandler(app, state, viewManager, fileOperations, null, _ => { });

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenTableIsNull_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        using var view = new TestTableView { Table = null };
        window.Add(view);
        var modeController = new ModeController(state);
        var fileOperations = new FileOperationsService(app, state, viewManager, modeController);
        using var handler = new AppKeyHandler(app, state, viewManager, fileOperations, null, _ => { });

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenGetRawColumnNameIsNull_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        using var view = new TestTableView { Table = new TestTableSource() };
        window.Add(view);
        var modeController = new ModeController(state);
        var fileOperations = new FileOperationsService(app, state, viewManager, modeController);
        using var handler = new AppKeyHandler(app, state, viewManager, fileOperations, null, _ => { });

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenOnMorphActionIsNull_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        using var view = new TestTableView
        {
            Table = new TestTableSource(),
            GetRawColumnName = _ => "test"
        };
        window.Add(view);
        var modeController = new ModeController(state);
        var fileOperations = new FileOperationsService(app, state, viewManager, modeController);
        using var handler = new AppKeyHandler(app, state, viewManager, fileOperations, null, _ => { });

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleActionMenu_WhenSelectedColumnIsNegative_ReturnsFalse()
    {
        // Arrange
        using var app = CreateTestApp();
        using var state = new AppState();
        using var window = new Window();
        using var viewManager = new ViewManager(window, state, () => Task.CompletedTask);
        using var view = new TestTableView
        {
            Table = new TestTableSource(),
            GetRawColumnName = _ => "test",
            OnMorphAction = _ => { }
        };
        view.SelectedColumn = -1;
        window.Add(view);
        var modeController = new ModeController(state);
        var fileOperations = new FileOperationsService(app, state, viewManager, modeController);
        using var handler = new AppKeyHandler(app, state, viewManager, fileOperations, null, _ => { });

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
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
}
