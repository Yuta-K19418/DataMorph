using AwesomeAssertions;
using DataMorph.App.Views;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App.Views;

public sealed class ColumnActionHandlerTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public void GetAvailableActions_Always_ReturnsAllSixActions()
    {
        // Arrange
        // Act
        var actions = ColumnActionHandler.GetAvailableActions();

        // Assert
        actions.Should().HaveCount(6);
        actions.Should().Contain(["Rename", "Delete", "Cast", "Filter", "Fill", "Format Timestamp"]);
    }

    [Fact]
    public void ExecuteAction_WithUnknownAction_DoesNotCallOnMorphAction()
    {
        // Arrange
        using var app = CreateTestApp();
        var table = new TableSource();
        var actionCalled = false;
        var handler = new ColumnActionHandler(
            app, table, 0,
            _ => "test",
            _ => actionCalled = true);

        // Act
        handler.ExecuteAction("UnknownAction");

        // Assert
        actionCalled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Rename")]
    [InlineData("Delete")]
    [InlineData("Cast")]
    [InlineData("Filter")]
    [InlineData("Fill")]
    [InlineData("Format Timestamp")]
    public void ExecuteAction_WithEachValidAction_DoesNotThrow(string action)
    {
        // Arrange
        using var app = CreateTestApp();
        var table = new TableSource();
        var handler = new ColumnActionHandler(
            app, table, 0,
            _ => "test",
            _ => { });
        app.StopAfterFirstIteration = true;

        // Act
        var exception = Record.Exception(() => handler.ExecuteAction(action));

        // Assert
        exception.Should().BeNull();
    }

    private sealed class TableSource : ITableSource
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
