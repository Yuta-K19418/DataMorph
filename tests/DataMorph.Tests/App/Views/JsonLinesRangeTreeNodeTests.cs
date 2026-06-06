using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO;
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

    private RowIndexer CreateAndBuildIndexer(string content)
    {
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();
        return indexer;
    }

    [Fact]
    public void Constructor_WithNullIndexer_ThrowsArgumentNullException()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);
        IRowIndexer? nullIndexer = null;

        // Act
        var act = () => new JsonLinesRangeTreeNode(nullIndexer!, reader, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullReader_ThrowsArgumentNullException()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        RowReader? reader = null;

        // Act
        var act = () => new JsonLinesRangeTreeNode(indexer, reader!, 0, 10);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeStartIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);

        // Act
        var act = () => new JsonLinesRangeTreeNode(indexer, reader, -1, 10);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithNegativeCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);

        // Act
        var act = () => new JsonLinesRangeTreeNode(indexer, reader, 0, -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);

        // Act
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 1000);

        // Assert
        node.Text.Should().Be("Lines 1-1000");
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText_PartialRange()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);

        // Act
        var node = new JsonLinesRangeTreeNode(indexer, reader, 1000, 500);

        // Assert
        node.Text.Should().Be("Lines 1001-1500");
    }

    [Fact]
    public void EnsureChildrenLoaded_PopulatesChildren()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("{\"a\":1}\n{\"b\":2}\n{\"c\":3}");
        using var reader = new RowReader(indexer.FilePath);
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 3);

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
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 0);

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
        var indexer = CreateAndBuildIndexer("{\"a\":1}\n{\"b\":2}\n{\"c\":3}");
        using var reader = new RowReader(indexer.FilePath);
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 3);

        // Act
        node.EnsureChildrenLoaded();
        var firstCount = node.Children.Count;
        node.EnsureChildrenLoaded();
        var secondCount = node.Children.Count;

        // Assert
        secondCount.Should().Be(firstCount);
    }

    [Fact]
    public void EnsureChildrenLoaded_CountExceedsAvailableLines_ReturnsOnlyAvailableChildren()
    {
        // Arrange — file has 2 lines, but node requests count=3; ReadLineBytes returns only 2
        var indexer = CreateAndBuildIndexer("{\"a\":1}\n{\"b\":2}");
        using var reader = new RowReader(indexer.FilePath);
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 3);

        // Act
        node.EnsureChildrenLoaded();
        var children = node.Children;

        // Assert
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
    [InlineData("{\"a\":1}", typeof(JsonObjectTreeNode), "Line 1: {Object: 1 properties}")]
    [InlineData("[1,2]", typeof(JsonArrayTreeNode), "Line 1: [Array: 2 items]")]
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
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 1);

        // Act
        var result = node.IsChildrenLoaded;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EnsureChildrenLoaded_LoadsChildren()
    {
        // Arrange
        var indexer = CreateAndBuildIndexer("{\"a\":1}\n{\"b\":2}");
        using var reader = new RowReader(indexer.FilePath);
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 2);

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
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 1);

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
        var indexer = CreateAndBuildIndexer("{\"a\":1}");
        using var reader = new RowReader(indexer.FilePath);
        var node = new JsonLinesRangeTreeNode(indexer, reader, 0, 0);

        // Act
        node.EnsureChildrenLoaded();

        // Assert
        node.IsChildrenLoaded.Should().BeTrue();
        node.Children.Should().BeEmpty();
    }
}
