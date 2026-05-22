using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO.JsonArray;

namespace DataMorph.Tests.App.Views;

public sealed class JsonArrayRangeTreeNodeTests : IDisposable
{
    private readonly string _testFilePath;
    private bool _disposed;

    public JsonArrayRangeTreeNodeTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_rangetreenode_{Guid.NewGuid()}.json"
        );
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            _disposed = true;
        }
    }

    private ElementByteCache CreateCache(string jsonContent)
    {
        File.WriteAllText(_testFilePath, jsonContent);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();
        return new ElementByteCache(indexer);
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange
        ElementByteCache? cache = null;

        // Act
        var act = () => new JsonArrayRangeTreeNode(cache!, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var cache = CreateCache("[1]");

        // Act
        var act = () => new JsonArrayRangeTreeNode(cache, -1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var cache = CreateCache("[1]");

        // Act
        var act = () => new JsonArrayRangeTreeNode(cache, 0, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText()
    {
        // Arrange
        using var cache = CreateCache("[1,2,3]");

        // Act
        var node = new JsonArrayRangeTreeNode(cache, 0, 1000);

        // Assert
        node.Text.Should().Be("[0 - 999]");
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText_PartialRange()
    {
        // Arrange
        using var cache = CreateCache("[1,2,3]");

        // Act
        var node = new JsonArrayRangeTreeNode(cache, 1000, 500);

        // Assert
        node.Text.Should().Be("[1000 - 1499]");
    }

    [Fact]
    public void Children_FirstAccess_LoadsElementNodes()
    {
        // Arrange
        using var cache = CreateCache("[1, 2, 3]");
        var node = new JsonArrayRangeTreeNode(cache, 0, 3);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(3);
    }

    [Fact]
    public void Children_EmptyRange_ReturnsEmptyChildren()
    {
        // Arrange
        using var cache = CreateCache("[1]");
        var node = new JsonArrayRangeTreeNode(cache, 0, 0);

        // Act
        var children = node.Children;

        // Assert
        node.Text.Should().Be("[0 - (empty)]");
        children.Should().BeEmpty();
    }

    [Fact]
    public void Children_ObjectElement_CreatesJsonObjectTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[{\"a\":1}]");
        var node = new JsonArrayRangeTreeNode(cache, 0, 1);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonObjectTreeNode>();
        children[0].Text.Should().StartWith("[0]: ");
    }

    [Fact]
    public void Children_ArrayElement_CreatesJsonArrayTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[[1,2]]");
        var node = new JsonArrayRangeTreeNode(cache, 0, 1);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonArrayTreeNode>();
        children[0].Text.Should().StartWith("[0]: ");
    }

    [Fact]
    public void Children_PrimitiveElement_CreatesJsonValueTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[42]");
        var node = new JsonArrayRangeTreeNode(cache, 0, 1);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(1);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be("[0]: 42");
    }

    [Fact]
    public void Children_NullElement_CreatesJsonValueTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[null, 1]");
        var node = new JsonArrayRangeTreeNode(cache, 0, 2);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(2);
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be("[0]: <null>");
    }

    [Fact]
    public void CreateElementNode_WithEmptyBytes_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        var emptyBytes = ReadOnlyMemory<byte>.Empty;

        // Act
        var elementNode = JsonArrayRangeTreeNode.CreateElementNode(emptyBytes, 0);

        // Assert
        elementNode.Should().BeOfType<JsonValueTreeNode>();
        elementNode.Text.Should().Contain("[Invalid JSON]");
    }

    [Fact]
    public void CreateElementNode_WithMalformedJson_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        var malformed = new ReadOnlyMemory<byte>(System.Text.Encoding.UTF8.GetBytes("{abc"));

        // Act
        var node = JsonArrayRangeTreeNode.CreateElementNode(malformed, 5);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
        node.Text.Should().StartWith("[5]: ");
    }

    [Fact]
    public void CreateElementNode_WithTruncatedObject_CreatesJsonValueTreeNodeWithErrorText()
    {
        // Arrange
        var truncated = new ReadOnlyMemory<byte>(System.Text.Encoding.UTF8.GetBytes("{"));

        // Act
        var node = JsonArrayRangeTreeNode.CreateElementNode(truncated, 3);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
        node.Text.Should().StartWith("[3]: ");
    }

    [Fact]
    public void Children_SecondAccess_ReturnsSameCount()
    {
        // Arrange
        using var cache = CreateCache("[1, 2, 3]");
        var node = new JsonArrayRangeTreeNode(cache, 0, 3);

        // Act
        var firstCount = node.Children.Count;
        var secondCount = node.Children.Count;

        // Assert
        secondCount.Should().Be(firstCount);
    }

    [Fact]
    public void Children_SkipsEmptyBytes_WhenCacheReturnsEmpty()
    {
        // Arrange — create a file with 2 elements, then request startIndex=0, count=3
        // index 0 and 1 have data, index 2 is beyond TotalRows so GetRow returns Empty
        using var cache = CreateCache("[1, 2]");
        var node = new JsonArrayRangeTreeNode(cache, 0, 3);

        // Act
        var children = node.Children;

        // Assert — index 2 returns Empty and is skipped
        children.Should().HaveCount(2);
    }
}
