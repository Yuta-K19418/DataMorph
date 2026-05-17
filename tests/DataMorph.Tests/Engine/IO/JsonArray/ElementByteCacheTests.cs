using DataMorph.Engine.IO.JsonArray;

namespace DataMorph.Tests.Engine.IO.JsonArray;

public sealed class ElementByteCacheTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly RowIndexer _indexer;
    private bool _disposed;

    public ElementByteCacheTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_elementbytecache_{Guid.NewGuid()}.json"
        );

        string[] elements =
        [
            "{\"id\":1,\"name\":\"Alice\"}",
            "{\"id\":2,\"name\":\"Bob\"}",
            "{\"id\":3,\"name\":\"Charlie\"}",
            "{\"id\":4,\"name\":\"David\"}",
            "{\"id\":5,\"name\":\"Eve\"}",
            "{\"id\":6,\"name\":\"Frank\"}",
            "{\"id\":7,\"name\":\"Grace\"}",
            "{\"id\":8,\"name\":\"Henry\"}",
            "{\"id\":9,\"name\":\"Ivy\"}",
            "{\"id\":10,\"name\":\"Jack\"}",
        ];

        File.WriteAllText(_testFilePath, $"[{string.Join(",", elements)}]");

        _indexer = new RowIndexer(_testFilePath);
        _indexer.BuildIndex();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
        _disposed = true;
    }

    [Fact]
    public void GetRow_WithinCachedRange_ReturnsCachedBytes()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void GetRow_OutsideCachedRange_UpdatesCacheWindow()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void GetRow_NegativeIndex_ReturnsEmpty()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void GetRow_IndexEqualToTotalElements_ReturnsEmpty()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void GetRow_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange

        // Act

        // Assert
    }
}
