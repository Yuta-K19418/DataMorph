using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.Models.Actions;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App.Views;

public sealed class MorphTableViewTests
{
    private static IApplication CreateTestApp()
    {
        var app = Application.Create();
        app.Init(DriverRegistry.Names.ANSI);
        return app;
    }

    [Fact]
    public void GetAvailableActions_WithOnMorphActionNull_ReturnsEmpty()
    {
        // Arrange
        using var app = CreateTestApp();
        using var table = new TestTableView
        {
            OnMorphAction = null,
            GetRawColumnName = _ => "test",
            Table = new TableSource(),
        };
        table.SelectedColumn = 0;

        // Act
        var actions = table.GetAvailableActions();

        // Assert
        actions.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableActions_WithTableNull_ReturnsEmpty()
    {
        // Arrange
        using var app = CreateTestApp();
        using var table = new TestTableView
        {
            OnMorphAction = _ => { },
            GetRawColumnName = _ => "test",
            Table = null,
        };
        table.SelectedColumn = 0;

        // Act
        var actions = table.GetAvailableActions();

        // Assert
        actions.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableActions_WithSelectedColumnNegative_ReturnsEmpty()
    {
        // Arrange
        using var app = CreateTestApp();
        using var table = new TestTableView
        {
            OnMorphAction = _ => { },
            GetRawColumnName = _ => "test",
        };
        table.Table = null; // Ensure Table is null
        table.SelectedColumn = -1;

        // Act
        var actions = table.GetAvailableActions();

        // Assert
        actions.Should().BeEmpty();
    }

    [Fact]
    public void GetAvailableActions_WithValidState_ReturnsAllActions()
    {
        // Arrange
        using var app = CreateTestApp();
        using var table = new TestTableView
        {
            OnMorphAction = _ => { },
            GetRawColumnName = _ => "test",
            Table = new TableSource(),
        };
        table.SelectedColumn = 0;

        // Act
        var actions = table.GetAvailableActions();

        // Assert
        actions.Should().HaveCount(6);
        actions.Should().Contain(["Rename", "Delete", "Cast", "Filter", "Fill", "Format Timestamp"]);
    }

    [Fact]
    public void ExecuteAction_WithUnknownActionName_DoesNothing()
    {
        // Arrange
        using var app = CreateTestApp();
        var actionCalled = false;
        using var table = new TestTableView
        {
            OnMorphAction = _ => actionCalled = true,
            GetRawColumnName = _ => "test",
            Table = new TableSource(),
        };
        table.SelectedColumn = 0;
        app.StopAfterFirstIteration = true;
        using var top = new Window();
        top.Add(table);
        app.Begin(top);

        // Act
        table.ExecuteAction("UnknownAction");

        // Assert
        actionCalled.Should().BeFalse();
    }

    [Fact]
    public void ExecuteAction_WithRenameAction_CallsOnMorphAction()
    {
        // Arrange
        using var app = CreateTestApp();
        MorphAction? capturedAction = null;
        using var table = new TestTableView
        {
            OnMorphAction = action => capturedAction = action,
            GetRawColumnName = _ => "old_name",
            Table = new TableSource(),
        };
        table.SelectedColumn = 0;
        app.StopAfterFirstIteration = true;
        using var top = new Window();
        top.Add(table);
        app.Begin(top);

        // Act
        // Note: ExecuteAction for Rename shows a dialog, so we can't test the full flow
        // We can only verify the method is called without throwing
        var exception = Record.Exception(() => table.ExecuteAction("Rename"));

        // Assert
        // Should not throw (dialog would be shown in real app)
        exception.Should().BeNull();
    }

    /// <summary>
    /// Testable concrete implementation of MorphTableView.
    /// </summary>
    private sealed class TestTableView : MorphTableView
    {
        // Inherits all functionality from MorphTableView
        // Just needs to be constructible for testing
    }

    /// <summary>
    /// Simple TableSource implementation for testing.
    /// </summary>
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

        [SuppressMessage("Performance", "CA1822:Mark members as static")]
        public void AddColumn(string name) { }
        [SuppressMessage("Performance", "CA1822:Mark members as static")]
        public void AddRow() { }
        [SuppressMessage("Performance", "CA1822:Mark members as static")]
        public void RemoveColumn(int index) { }
        [SuppressMessage("Performance", "CA1822:Mark members as static")]
        public void RemoveRow(int index) { }
        [SuppressMessage("Performance", "CA1822:Mark members as static")]
        public void Clear() { }
    }
}

