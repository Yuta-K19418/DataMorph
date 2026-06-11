using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.App.Views.JsonRangeTreeNodes;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.App.Views;

public sealed partial class JsonLinesTreeViewTests
{
    [Fact]
    public void Create_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = CreateTestApp();

        // Act
        var act = () => JsonLinesTreeView.Create(null!, () => { }, NoOpUiThreadInvoke);

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
        var act = () => JsonLinesTreeView.Create(indexer, null!, NoOpUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = CreateTestApp();
        var filePath = CreateTempFile("{\"a\":1}");
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        var act = () => JsonLinesTreeView.Create(indexer, () => { }, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
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
        using var view = JsonLinesTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonLinesTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonLinesTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonLinesTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonLinesTreeView.Create(stubIndexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonLinesTreeView.Create(stubIndexer, () => { }, NoOpUiThreadInvoke);

        // Assert — only 2 nodes added (Empty for index 2 is skipped)
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(2);
    }

    [Fact]
    public void Create_IndexingCompleted_SmallFile_AddsLineNodesDirectly()
    {
        // Arrange — indexer has already completed indexing with TotalRows ≤ 1000

        // Act

        // Assert
    }

    [Fact]
    public void Create_IndexingCompleted_LargeFile_AddsRangeNodes()
    {
        // Arrange — indexer has already completed indexing with TotalRows > 1000

        // Act

        // Assert
    }

    [Fact]
    public void Create_IndexingCompleted_VeryLargeFile_AddsSuperRangeNodes()
    {
        // Arrange — indexer completed, file size suggests > 1M estimated rows

        // Act

        // Assert
    }

    [Fact]
    public void Create_IndexingInProgress_SubscribesToProgressChanged()
    {
        // Arrange — indexer is still building (IsIndexingCompleted == false)

        // Act

        // Assert
    }

    [Fact]
    public void Create_IndexingInProgress_TOCTOU_CompletedBeforeSubscribe()
    {
        // Arrange — indexer completes between FirstCheckpointReached and Create() call

        // Act

        // Assert
    }
}
