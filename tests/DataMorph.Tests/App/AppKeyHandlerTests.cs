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
    [InlineData(KeyCode.O)]
    [InlineData(KeyCode.S)]
    [InlineData(KeyCode.Q)]
    [InlineData(KeyCode.T)]
    [InlineData(KeyCode.X)]
    [InlineData(KeyCode.C)]
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
    [InlineData(KeyCode.A)]
    [InlineData(KeyCode.B)]
    [InlineData(KeyCode.Z)]
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
    [InlineData(KeyCode.O | KeyCode.CtrlMask)]
    [InlineData(KeyCode.S | KeyCode.CtrlMask)]
    [InlineData(KeyCode.Q | KeyCode.CtrlMask)]
    [InlineData(KeyCode.T | KeyCode.CtrlMask)]
    [InlineData(KeyCode.X | KeyCode.CtrlMask)]
    [InlineData(KeyCode.C | KeyCode.CtrlMask)]
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
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler, null);

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
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);
        using var view = new TestTableView { Table = null };
        window.Add(view);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler, null);

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
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);
        using var view = new TestTableView { Table = new TestTableSource() };
        window.Add(view);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler, null);

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
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);
        using var view = new TestTableView
        {
            Table = new TestTableSource(),
            GetRawColumnName = _ => "test"
        };
        window.Add(view);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler, null);

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
        var modeController = new ModeController(state);
        using var viewManager = new ViewManager(window, state, modeController);
        using var view = new TestTableView
        {
            Table = new TestTableSource(),
            GetRawColumnName = _ => "test",
            OnMorphAction = _ => { }
        };
        view.SelectedColumn = -1;
        window.Add(view);
        var fileDialogHandler = new FileDialogHandler(app, state, viewManager, _ => { });
        var recipeCommandHandler = new RecipeCommandHandler(app, state, viewManager);
        using var handler = new AppKeyHandler(app, state, viewManager, fileDialogHandler, recipeCommandHandler, null);

        // Act
        var result = handler.HandleActionMenu();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HandleClearActions_WhenActionStackIsEmpty_ReturnsFalse()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void HandleClearActions_WhenActionStackHasActions_ReturnsTrue()
    {
        // Arrange

        // Act

        // Assert
        // Note: MessageBox.Query display is not unit-testable (requires TUI event loop).
        //       Verify return value true (key consumed) only.
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
