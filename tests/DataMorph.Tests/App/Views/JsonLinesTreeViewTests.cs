using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonLines;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace DataMorph.Tests.App.Views;

public sealed class JsonLinesTreeViewTests : IDisposable
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

    private string CreateTempFile(string content)
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonlines_treeview_{Guid.NewGuid()}.jsonl"
        );
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
    public void Create_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = CreateTestApp();

        // Act
        var act = () => JsonLinesTreeView.Create(null!, () => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnTableModeToggle_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = CreateTestApp();
        var filePath = CreateTempFile("{\"a\":1}");
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        var act = () => JsonLinesTreeView.Create(indexer, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithTotalRowsExceedingIntMax_ThrowsNotSupportedException()
    {
        // Arrange
        using var app = CreateTestApp();
        var filePath = CreateTempFile("{\"a\":1}");
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        // Stub says TotalRows exceeds int.MaxValue
        var stubIndexer = new StubRowIndexer(realIndexer, (long)int.MaxValue + 1);

        // Act
        var act = () => JsonLinesTreeView.Create(stubIndexer, () => { });

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Create_SmallFile_AddsLineNodesDirectly()
    {
        // Arrange
        var filePath = CreateTempFile("{\"a\":1}\n{\"b\":2}\n{\"c\":3}");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonLinesTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(3);
        list.Should().NotContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_ExactBoundary_AddsLineNodesDirectly()
    {
        // Arrange — 1000 lines is exactly the boundary, uses direct line nodes
        var lines = Enumerable.Range(0, 1000)
            .Select(i => $"{{\"id\":{i}}}");
        var filePath = CreateTempFile(string.Join("\n", lines));
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonLinesTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(1000);
        list.Should().NotContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_LargeFile_AddsRangeNodes()
    {
        // Arrange — 1001 lines triggers range mode
        var lines = Enumerable.Range(0, 1001)
            .Select(i => $"{{\"id\":{i}}}");
        var filePath = CreateTempFile(string.Join("\n", lines));
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonLinesTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(2);
        list.Should().OnlyContain(o => o is JsonLinesRangeTreeNode);
    }

    [Fact]
    public void Create_LargeFile_CorrectRangeCount()
    {
        // Arrange — 2500 lines → 3 ranges: [0-999], [1000-1999], [2000-2499]
        var lines = Enumerable.Range(0, 2500)
            .Select(i => $"{{\"id\":{i}}}");
        var filePath = CreateTempFile(string.Join("\n", lines));
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonLinesTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(3);
        list[0].Text.Should().Be("Lines 1-1000");
        list[1].Text.Should().Be("Lines 1001-2000");
        list[2].Text.Should().Be("Lines 2001-2500");
    }

    [Fact]
    public void Create_EmptyFile_AddsNoNodes()
    {
        // Arrange — non-empty file so RowReader can mmap, but stub reports 0 rows
        using var app = CreateTestApp();
        var filePath = CreateTempFile(" \n");
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        var stubIndexer = new StubRowIndexer(realIndexer, 0);

        // Act
        using var view = JsonLinesTreeView.Create(stubIndexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().BeEmpty();
    }

    [Fact]
    public void Create_SmallFile_SkipsEmptyBytes_WhenCacheReturnsEmpty()
    {
        // Arrange
        using var app = CreateTestApp();
        var filePath = CreateTempFile("{\"a\":1}\n{\"b\":2}");
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        // Stub says TotalRows=3 but file only has 2 lines → GetRow(2) returns Empty
        var stubIndexer = new StubRowIndexer(realIndexer, 3);

        // Act
        using var view = JsonLinesTreeView.Create(stubIndexer, () => { });

        // Assert — only 2 nodes added (Empty for index 2 is skipped)
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(2);
    }

    [Fact]
    public void HandleAccepted_WhenRangeNodeCollapsed_ClearsAndRecreatesChildren()
    {
        // Arrange
        using var app = CreateTestApp();
        var lines = Enumerable.Range(0, 1001)
            .Select(i => $"{{\"id\":{i}}}");
        var filePath = CreateTempFile(string.Join("\n", lines));
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        using var view = JsonLinesTreeView.Create(indexer, () => { });
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var rangeNode = objects.OfType<JsonLinesRangeTreeNode>().First();

        // Expand the range node via Accept command — lazy loads children
        view.SelectedObject = rangeNode;
        view.InvokeCommand(Command.Accept);
        var beforeCollapse = rangeNode.Children;
        beforeCollapse.Should().NotBeEmpty();

        // Act — collapse
        view.InvokeCommand(Command.Accept);

        // Assert — ClearChildren() was called: Children is now empty
        rangeNode.Children.Should().BeEmpty();

        // Act — simulate re-expansion via TreeBuilder's childGetter
        rangeNode.EnsureChildrenLoaded();

        // Assert — children are reloaded as a new instance
        rangeNode.Children.Should().NotBeSameAs(beforeCollapse);
        rangeNode.Children.Should().NotBeEmpty();
    }

    [Fact]
    public void HandleAccepted_NonRangeNode_DoesNotThrow()
    {
        // Arrange
        var filePath = CreateTempFile("{\"a\":1}");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();
        using var view = JsonLinesTreeView.Create(indexer, () => { });
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var lineNode = objects.First();
        view.SelectedObject = lineNode;

        // Act — Accept on a non-range node should not throw
        var act = () => view.InvokeCommand(Command.Accept);

        // Assert
        act.Should().NotThrow();
    }

    /// <summary>
    /// Stub IRowIndexer that overrides TotalRows while delegating everything else to the inner indexer.
    /// </summary>
    private sealed class StubRowIndexer(IRowIndexer inner, long fakeTotalRows) : IRowIndexer
    {
        public string FilePath => inner.FilePath;
        public long FileSize => inner.FileSize;
        public long BytesRead => inner.BytesRead;
        public long TotalRows => fakeTotalRows;
        public bool IsIndexingCompleted => inner.IsIndexingCompleted;

#pragma warning disable CS0067
        public event Action? FirstCheckpointReached;
        public event Action<long, long>? ProgressChanged;
        public event Action? BuildIndexCompleted;
#pragma warning restore CS0067

        public void BuildIndex(CancellationToken ct = default) => inner.BuildIndex(ct);

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) =>
            inner.GetCheckPoint(targetRow);
    }
}
