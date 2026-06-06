using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.App.Views;

public sealed class JsonLinesRangeTreeNodeTests : IDisposable
{
    private readonly string _testFilePath;
    private bool _disposed;

    public JsonLinesRangeTreeNodeTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonlines_rangetreenode_{Guid.NewGuid()}.jsonl"
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

    private RowByteCache CreateCache(string content)
    {
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();
        return new RowByteCache(indexer);
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Arrange
        RowByteCache? cache = null;

        // Act
        var act = () => new JsonLinesRangeTreeNode(cache!, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}");

        // Act
        var act = () => new JsonLinesRangeTreeNode(cache, -1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}");

        // Act
        var act = () => new JsonLinesRangeTreeNode(cache, 0, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}");

        // Act
        var node = new JsonLinesRangeTreeNode(cache, 0, 1000);

        // Assert
        node.Text.Should().Be("Lines 1-1000");
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText_PartialRange()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}");

        // Act
        var node = new JsonLinesRangeTreeNode(cache, 1000, 500);

        // Assert
        node.Text.Should().Be("Lines 1001-1500");
    }

    [Fact]
    public void EnsureChildrenLoaded_PopulatesChildren()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}\n{\"b\":2}\n{\"c\":3}");
        var node = new JsonLinesRangeTreeNode(cache, 0, 3);

        // Act
        node.EnsureChildrenLoaded();
        var children = node.Children;

        // Assert
        children.Should().HaveCount(3);
    }

    [Fact]
    public void Children_EmptyRange_ReturnsEmptyChildren()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}");
        var node = new JsonLinesRangeTreeNode(cache, 0, 0);

        // Act
        var children = node.Children;

        // Assert
        node.Text.Should().Be("Lines 1 (empty)");
        children.Should().BeEmpty();
    }

    [Fact]
    public void EnsureChildrenLoaded_CalledTwice_ReturnsSameCount()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}\n{\"b\":2}\n{\"c\":3}");
        var node = new JsonLinesRangeTreeNode(cache, 0, 3);

        // Act
        node.EnsureChildrenLoaded();
        var firstCount = node.Children.Count;
        node.EnsureChildrenLoaded();
        var secondCount = node.Children.Count;

        // Assert
        secondCount.Should().Be(firstCount);
    }

    [Fact]
    public void Children_SkipsEmptyBytes_WhenCacheReturnsEmpty()
    {
        // Arrange — create a file with 2 lines, then request startIndex=0, count=3
        // index 0 and 1 have data, index 2 is beyond TotalRows so GetRow returns Empty
        using var cache = CreateCache("{\"a\":1}\n{\"b\":2}");
        var node = new JsonLinesRangeTreeNode(cache, 0, 3);

        // Act
        node.EnsureChildrenLoaded();
        var children = node.Children;

        // Assert — index 2 returns Empty and is skipped
        children.Should().HaveCount(2);
    }

    [Fact]
    public void CreateLineNode_WithEmptyBytes_CreatesInvalidNode()
    {
        // Arrange
        var emptyBytes = ReadOnlyMemory<byte>.Empty;

        // Act
        var node = JsonLinesRangeTreeNode.CreateLineNode(emptyBytes, 0);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
    }

    [Fact]
    public void CreateLineNode_WithMalformedJson_CreatesInvalidNode()
    {
        // Arrange
        var malformed = new ReadOnlyMemory<byte>("{abc"u8.ToArray());

        // Act
        var node = JsonLinesRangeTreeNode.CreateLineNode(malformed, 5);

        // Assert
        node.Should().BeOfType<JsonValueTreeNode>();
        node.Text.Should().Contain("[Invalid JSON]");
        node.Text.Should().StartWith("Line 6:");
    }

    [Fact]
    public void CreateLineNode_SetsLineNumberOnJsonObjectTreeNode()
    {
        // Arrange
        var json = "{\"a\":1}"u8.ToArray();
        var bytes = new ReadOnlyMemory<byte>(json);

        // Act
        var node = JsonLinesRangeTreeNode.CreateLineNode(bytes, 0);

        // Assert
        node.Should().BeOfType<JsonObjectTreeNode>();
        var objNode = (JsonObjectTreeNode)node;
        objNode.LineNumber.Should().Be(1);
    }

    [Theory]
    [InlineData("{\"a\":1}", typeof(JsonObjectTreeNode), "Line 1: {...}")]
    [InlineData("[1,2]", typeof(JsonArrayTreeNode), "Line 1: [...]")]
    [InlineData("42", typeof(JsonValueTreeNode), "Line 1: 42")]
    public void CreateLineNode_ByTokenType_CreatesCorrectNodeAndLabel(
        string json, Type expectedType, string expectedLabel)
    {
        // Arrange
        var bytes = new ReadOnlyMemory<byte>(System.Text.Encoding.UTF8.GetBytes(json));

        // Act
        var node = JsonLinesRangeTreeNode.CreateLineNode(bytes, 0);

        // Assert
        node.Should().BeOfType(expectedType);
        node.Text.Should().Be(expectedLabel);
    }

    [Fact]
    public void IsChildrenLoaded_InitiallyFalse()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}");
        var node = new JsonLinesRangeTreeNode(cache, 0, 1);

        // Act
        var result = node.IsChildrenLoaded;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EnsureChildrenLoaded_LoadsChildren()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}\n{\"b\":2}");
        var node = new JsonLinesRangeTreeNode(cache, 0, 2);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        node.IsChildrenLoaded.Should().BeTrue();
        node.Children.Should().HaveCount(2);
    }

    [Fact]
    public void EnsureChildrenLoaded_WhenAlreadyLoaded_IsIdempotent()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}");
        var node = new JsonLinesRangeTreeNode(cache, 0, 1);

        // Act
        node.EnsureChildrenLoaded();
        var firstChildren = node.Children;
        node.EnsureChildrenLoaded();
        var secondChildren = node.Children;

        // Assert — second call does not reload
        secondChildren.Should().BeSameAs(firstChildren);
    }

    [Fact]
    public void EnsureChildrenLoaded_EmptyRange_SetsIsChildrenLoadedTrue()
    {
        // Arrange
        using var cache = CreateCache("{\"a\":1}");
        var node = new JsonLinesRangeTreeNode(cache, 0, 0);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        node.IsChildrenLoaded.Should().BeTrue();
        node.Children.Should().BeEmpty();
    }
}
