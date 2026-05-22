using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.IO;
using DataMorph.Engine.IO.JsonArray;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace DataMorph.Tests.App.Views;

public sealed class JsonArrayTreeViewTests : IDisposable
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
        var filePath = Path.Combine(Path.GetTempPath(), $"jsonarray_treeview_{Guid.NewGuid()}.json");
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
        var act = () => JsonArrayTreeView.Create(null!, () => { });

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_SmallArray_AddsElementNodesDirectly()
    {
        // Arrange
        var filePath = CreateTempFile("[1, 2, 3, 4, 5]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(5);
        list.Should().NotContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_ExactBoundary_AddsElementNodesDirectly()
    {
        // Arrange — 1000 elements is exactly the boundary, uses direct element nodes
        var elements = Enumerable.Range(0, 1000)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(1000);
        list.Should().NotContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_LargeArray_AddsRangeNodes()
    {
        // Arrange — 1001 elements triggers range mode
        var elements = Enumerable.Range(0, 1001)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(2);
        list.Should().OnlyContain(o => o is JsonArrayRangeTreeNode);
    }

    [Fact]
    public void Create_LargeArray_CorrectRangeCount()
    {
        // Arrange — 2500 elements → 3 ranges: [0-999], [1000-1999], [2000-2499]
        var elements = Enumerable.Range(0, 2500)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        var list = objects.ToList();
        list.Should().HaveCount(3);
        list[0].Text.Should().Be("[0 - 999]");
        list[1].Text.Should().Be("[1000 - 1999]");
        list[2].Text.Should().Be("[2000 - 2499]");
    }

    [Fact]
    public void Create_EmptyArray_AddsNoNodes()
    {
        // Arrange
        var filePath = CreateTempFile("[]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { });

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().BeEmpty();
    }

    [Fact]
    public void Create_SmallArray_SkipsEmptyBytes_WhenCacheReturnsEmpty()
    {
        // Arrange
        using var app = CreateTestApp();
        var filePath = CreateTempFile("[1, 2]");
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        // Stub says TotalRows=3 but file only has 2 elements → GetRow(2) returns Empty
        var stubIndexer = new StubRowIndexer(realIndexer, 3);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { });

        // Assert — only 2 nodes added (Empty for index 2 is skipped)
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(2);
    }

    /// <summary>
    /// Stub IRowIndexer that overrides TotalRows while delegating everything else to the inner indexer.
    /// </summary>
    private sealed class StubRowIndexer(IRowIndexer inner, int fakeTotalRows) : IRowIndexer
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

        public (long byteOffset, int rowOffset) GetCheckPoint(long targetRow) => inner.GetCheckPoint(targetRow);
    }
}
