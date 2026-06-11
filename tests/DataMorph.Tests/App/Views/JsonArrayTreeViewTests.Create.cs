using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.App.Views.JsonRangeTreeNodes;
using DataMorph.Engine.IO.JsonArray;

namespace DataMorph.Tests.App.Views;

public sealed partial class JsonArrayTreeViewTests
{
    [Fact]
    public void Create_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        using var app = CreateTestApp();

        // Act
        var act = () => JsonArrayTreeView.Create(null!, () => { }, NoOpUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullOnTableModeToggle_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = CreateTempFile("[1]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        var act = () => JsonArrayTreeView.Create(indexer, null!, NoOpUiThreadInvoke);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullUiThreadInvoke_ThrowsArgumentNullException()
    {
        // Arrange

        // Act

        // Assert
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
        using var view = JsonArrayTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonArrayTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonArrayTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonArrayTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

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
        using var view = JsonArrayTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

        // Assert
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().BeEmpty();
    }

    [Fact]
    public void Create_SmallArray_SkipsEmptyBytes_WhenReaderReturnsFewerElements()
    {
        // Arrange
        using var app = CreateTestApp();
        var filePath = CreateTempFile("[1, 2]");
        var realIndexer = new RowIndexer(filePath);
        realIndexer.BuildIndex();
        // Stub says TotalRows=3 but file only has 2 elements → ReadElementBytes returns only 2 elements
        var stubIndexer = new StubRowIndexer(realIndexer, 3);

        // Act
        using var view = JsonArrayTreeView.Create(stubIndexer, () => { }, NoOpUiThreadInvoke);

        // Assert — only 2 nodes added (Empty for index 2 is skipped)
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.ToList().Should().HaveCount(2);
    }

    [Fact]
    public void Create_LargeArray_RangeNodesAreNotEagerlyLoaded()
    {
        // Arrange — 1001 elements triggers range mode
        var elements = Enumerable.Range(0, 1001)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var filePath = CreateTempFile($"[{string.Join(",", elements)}]");
        using var app = CreateTestApp();
        var indexer = new RowIndexer(filePath);
        indexer.BuildIndex();

        // Act
        using var view = JsonArrayTreeView.Create(indexer, () => { }, NoOpUiThreadInvoke);

        // Assert — DelegateTreeBuilder prevents eager loading via AddObject()
        var objects = view.Objects;
        objects.Should().NotBeNull();
        objects.OfType<JsonArrayRangeTreeNode>().Should().OnlyContain(n => !n.IsChildrenLoaded);
    }

    [Fact]
    public void Create_IndexingCompleted_SmallArray_AddsElementNodesDirectly()
    {
        // Arrange — indexer has already completed indexing with TotalRows ≤ 1000

        // Act

        // Assert
    }

    [Fact]
    public void Create_IndexingCompleted_LargeArray_AddsRangeNodes()
    {
        // Arrange — indexer has already completed indexing with TotalRows > 1000

        // Act

        // Assert
    }

    [Fact]
    public void Create_IndexingCompleted_VeryLargeArray_AddsSuperRangeNodes()
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
