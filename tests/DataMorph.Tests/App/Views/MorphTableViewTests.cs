using AwesomeAssertions;
using DataMorph.App.Views;
using Terminal.Gui.Views;

namespace DataMorph.Tests.App.Views;

#pragma warning disable CA2000

public sealed class MorphTableViewTests
{
    private sealed class ConcreteMorphTableView : MorphTableView
    {
    }

    private sealed class DisposableTableSource : ITableSource, IDisposable
    {
        public bool IsDisposed { get; private set; }
        public int Rows => 0;
        public int Columns => 0;
        public string[] ColumnNames => [];
        public object this[int row, int col] => throw new NotImplementedException();
        public void Dispose() => IsDisposed = true;
    }

    private sealed class NonDisposableTableSource : ITableSource
    {
        public int Rows => 0;
        public int Columns => 0;
        public string[] ColumnNames => [];
        public object this[int row, int col] => throw new NotImplementedException();
    }

    [Fact]
    public void Dispose_DisposesTableIfIDisposable()
    {
        // Arrange
        var view = new ConcreteMorphTableView();
        var table = new DisposableTableSource();
        view.Table = table;

        // Act
        view.Dispose();

        // Assert
        table.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenTableIsNotIDisposable()
    {
        // Arrange
        var view = new ConcreteMorphTableView();
        var table = new NonDisposableTableSource();
        view.Table = table;

        // Act
        var act = () => view.Dispose();

        // Assert
        act.Should().NotThrow();
    }
}
