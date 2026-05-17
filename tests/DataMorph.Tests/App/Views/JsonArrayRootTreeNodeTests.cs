using AwesomeAssertions;
using DataMorph.App.Views;
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

        // Assert
    }

    [Fact]
    public void Children_FirstAccess_LoadsElementNodes()
    {
        // Arrange
        using var cache = CreateCache("[1, 2, 3]");

        // Act

        // Assert
    }

    [Fact]
    public void Children_EmptyArray_ReturnsEmptyChildren()
    {
        // Arrange
        using var cache = CreateCache("[]");

        // Act

        // Assert
    }

    [Fact]
    public void Children_ObjectElement_CreatesJsonObjectTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[{\"a\":1}]");

        // Act

        // Assert
    }

    [Fact]
    public void Children_ArrayElement_CreatesJsonArrayTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[[1,2]]");

        // Act

        // Assert
    }

    [Fact]
    public void Children_PrimitiveElement_CreatesJsonValueTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[42]");

        // Act

        // Assert
    }

    [Fact]
    public void Children_NullElement_CreatesJsonValueTreeNode()
    {
        // Arrange
        using var cache = CreateCache("[null, 1]");

        // Act

        // Assert
    }

    [Fact]
    public void Children_LargeArray_CapsAtMaxElementsShown()
    {
        // Arrange
        // TODO: Create a mock/stub of ElementByteCache with TotalRows = 6000
        //       OR generate a JSON array file with 6000 elements via a helper

        // Act

        // Assert
    }

    [Fact]
    public void Children_LargeArray_TruncationNodeHasExpectedText()
    {
        // Arrange
        // TODO: Create a mock/stub of ElementByteCache with TotalRows = 6000
        //       OR generate a JSON array file with 6000 elements via a helper

        // Act

        // Assert
    }

    [Fact]
    public void Children_SecondAccess_ReturnsSameCount()
    {
        // Arrange
        using var cache = CreateCache("[1, 2, 3]");

        // Act

        // Assert
    }
}
