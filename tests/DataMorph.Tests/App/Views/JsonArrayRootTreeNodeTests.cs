using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.App.Views.JsonTreeNodes;
using DataMorph.Engine.IO.JsonArray;

namespace DataMorph.Tests.App.Views;

public sealed class JsonArrayRootTreeNodeTests : IDisposable
{
    private readonly string _testFilePath;
    private bool _disposed;

    public JsonArrayRootTreeNodeTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_roottreenode_{Guid.NewGuid()}.json"
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

    private ElementByteCache CreateLargeCache(int elementCount)
    {
        var elements = Enumerable.Range(0, elementCount)
            .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");
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
        var act = () => new JsonArrayRootTreeNode(cache!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_SetsCorrectDisplayText()
    {
        // Arrange
        using var cache = CreateCache("[1,2,3,4,5]");

        // Act
        var node = new JsonArrayRootTreeNode(cache);

        // Assert
        node.Text.Should().Be("[ 5 items ]");
    }

    [Fact]
    public void Children_FirstAccess_LoadsElementNodes()
    {
        // Arrange
        using var cache = CreateCache("[1, 2, 3]");
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(3);
    }

    [Fact]
    public void Children_EmptyArray_ReturnsEmptyChildren()
    {
        // Arrange — file must be non-empty for MmapService; use a minimal valid array
        // with a single element but then index with 0 elements via a fresh empty array.
        // Since MmapService rejects empty files, use "[]" which is 2 bytes.
        using var cache = CreateCache("[]");
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert
        children.Should().BeEmpty();
    }

    [Fact]
    public void Children_ObjectElement_CreatesJsonObjectTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[{\"a\":1}]");
        var node = new JsonArrayRootTreeNode(cache);

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
        var node = new JsonArrayRootTreeNode(cache);

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
        var node = new JsonArrayRootTreeNode(cache);

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
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be("[0]: <null>");
    }

    [Fact]
    public void Children_LargeArray_CapsAtMaxElementsShown()
    {
        // Arrange
        using var cache = CreateLargeCache(6000);
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert — MaxElementsShown elements + 1 truncation node
        children.Should().HaveCount(JsonArrayRootTreeNode.MaxElementsShown + 1);
    }

    [Fact]
    public void Children_StringElement_CreatesJsonValueTreeNodeWithQuotes()
    {
        // Arrange
        using var cache = CreateCache("[\"hello\"]");
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be("[0]: \"hello\"");
    }

    [Theory]
    [InlineData("[true]", "[0]: true")]
    [InlineData("[false]", "[0]: false")]
    public void Children_BoolElement_CreatesJsonValueTreeNodeWithBoolText(
        string json, string expectedText)
    {
        // Arrange
        using var cache = CreateCache(json);
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert
        children[0].Should().BeOfType<JsonValueTreeNode>();
        children[0].Text.Should().Be(expectedText);
    }

    [Fact]
    public void Children_AtMaxElementsShownBoundary_DoesNotAddTruncationNode()
    {
        // Arrange
        using var cache = CreateLargeCache(JsonArrayRootTreeNode.MaxElementsShown);
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(JsonArrayRootTreeNode.MaxElementsShown);
        children.Should().NotContain(c => c.Text.Contains("more element"));
    }

    [Fact]
    public void Children_OnePastMaxElementsShownBoundary_AddsTruncationNode()
    {
        // Arrange
        using var cache = CreateLargeCache(JsonArrayRootTreeNode.MaxElementsShown + 1);
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert
        children.Should().HaveCount(JsonArrayRootTreeNode.MaxElementsShown + 1);
        children[^1].Text.Should().Be("... (1 more element - use a filtered view)");
    }

    [Fact]
    public void Children_LargeArray_TruncationNodeHasExpectedText()
    {
        // Arrange
        using var cache = CreateLargeCache(JsonArrayRootTreeNode.MaxElementsShown + 1000);
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var children = node.Children;

        // Assert
        children[^1].Text.Should().Be("... (1000 more elements - use a filtered view)");
    }

    [Fact]
    public void Children_SecondAccess_ReturnsSameCount()
    {
        // Arrange
        using var cache = CreateCache("[1, 2, 3]");
        var node = new JsonArrayRootTreeNode(cache);

        // Act
        var firstCount = node.Children.Count;
        var secondCount = node.Children.Count;

        // Assert
        secondCount.Should().Be(firstCount);
    }
}
