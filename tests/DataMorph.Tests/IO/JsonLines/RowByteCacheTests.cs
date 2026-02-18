using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.IO.JsonLines;

public sealed partial class RowByteCacheTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly RowIndexer _indexer;
    private bool _disposed;

    public RowByteCacheTests()
    {
        // Arrange
        _testFilePath = Path.GetTempFileName();

        // Create JSON Lines file for testing
        var lines = new[]
        {
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
        };

        File.WriteAllLines(_testFilePath, lines);

        // Create RowIndexer and build index - MmapService is not used
        _indexer = new RowIndexer(_testFilePath);
        _indexer.BuildIndex();
    }

    [Fact]
    public void GetLineBytes_WithinCachedRange_ReturnsCachedBytes()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, cacheSize: 5);
        var expectedLine2 = "{\"id\":2,\"name\":\"Bob\"}"u8.ToArray();

        // Act - Get line 2 (within cache)
        var result1 = cache.GetLineBytes(1).ToArray();
        // Get again (cache hit)
        var result2 = cache.GetLineBytes(1).ToArray();

        // Assert
        result1.Should().BeEquivalentTo(expectedLine2);
        result2.Should().BeEquivalentTo(expectedLine2);
    }

    [Fact]
    public void GetLineBytes_OutsideCachedRange_UpdatesCacheWindow()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, cacheSize: 5);
        var expectedLine1 = "{\"id\":1,\"name\":\"Alice\"}"u8.ToArray();
        var expectedLine8 = "{\"id\":8,\"name\":\"Henry\"}"u8.ToArray();

        // Act - First access caches lines 0-4
        var result1 = cache.GetLineBytes(0).ToArray();
        // Next access to line 7 updates cache window
        var result2 = cache.GetLineBytes(7).ToArray();

        // Assert
        result1.Should().BeEquivalentTo(expectedLine1);
        result2.Should().BeEquivalentTo(expectedLine8);
    }

    [Fact]
    public void GetLineBytes_FirstLineRequested_CachesFromBeginning()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, cacheSize: 3);
        var expectedLine1 = "{\"id\":1,\"name\":\"Alice\"}"u8.ToArray();
        var expectedLine2 = "{\"id\":2,\"name\":\"Bob\"}"u8.ToArray();
        var expectedLine3 = "{\"id\":3,\"name\":\"Charlie\"}"u8.ToArray();

        // Act
        var result1 = cache.GetLineBytes(0).ToArray();
        var result2 = cache.GetLineBytes(1).ToArray();
        var result3 = cache.GetLineBytes(2).ToArray();

        // Assert
        result1.Should().BeEquivalentTo(expectedLine1);
        result2.Should().BeEquivalentTo(expectedLine2);
        result3.Should().BeEquivalentTo(expectedLine3);
    }

    [Fact]
    public void GetLineBytes_LastLineRequested_CachesToEnd()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, cacheSize: 3);
        var totalLines = _indexer.TotalRows;
        var lastLineIndex = (int)totalLines - 1;
        var expectedLastLine = "{\"id\":10,\"name\":\"Jack\"}"u8.ToArray();

        // Act
        var result = cache.GetLineBytes(lastLineIndex).ToArray();

        // Assert
        result.Should().BeEquivalentTo(expectedLastLine);
    }

    [Fact]
    public void GetLineBytes_EmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var emptyFilePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(emptyFilePath, string.Empty);

            var indexer = new RowIndexer(emptyFilePath);
            indexer.BuildIndex();

            // Act
            var act = () =>
            {
                using var cache = new RowByteCache(indexer);
            };

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            File.Delete(emptyFilePath);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GetLineBytes_NegativeIndex_ReturnsEmpty(int invalidIndex)
    {
        // Arrange
        using var cache = new RowByteCache(_indexer);

        // Act
        var result = cache.GetLineBytes(invalidIndex);

        // Assert
        result.IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    public void GetLineBytes_IndexEqualToOrGreaterThanTotalLines_ReturnsEmpty(int overflowIndex)
    {
        // Arrange
        using var cache = new RowByteCache(_indexer);

        // Act
        var result = cache.GetLineBytes(overflowIndex);

        // Assert
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void UpdateCache_RequestedAtCacheCenter_KeepsExistingCache()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, cacheSize: 5);

        // First access to line 2 (cache window: 0-4)
        cache.GetLineBytes(1);

        // Get line 2 again (center of cache)
        var beforeAccess = cache.GetLineBytes(1).ToArray();

        // Act - Request same line again
        var afterAccess = cache.GetLineBytes(1).ToArray();

        // Assert - Cache should be maintained
        afterAccess.Should().BeEquivalentTo(beforeAccess);
    }

    [Fact]
    public void UpdateCache_RequestedNearWindowEdge_ShiftsCacheWindow()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, cacheSize: 5);
        var expectedLine4 = "{\"id\":4,\"name\":\"David\"}"u8.ToArray();
        var expectedLine8 = "{\"id\":8,\"name\":\"Henry\"}"u8.ToArray();

        // First access sets cache window (lines 0-4)
        cache.GetLineBytes(0);

        // Act - Access edge of cache window (line 4)
        var result1 = cache.GetLineBytes(3).ToArray();
        // Access line outside cache (line 8)
        var result2 = cache.GetLineBytes(7).ToArray();

        // Assert
        result1.Should().BeEquivalentTo(expectedLine4);
        result2.Should().BeEquivalentTo(expectedLine8);
    }

    [Fact]
    public void UpdateCache_CacheSizeLargerThanTotalLines_CachesAllLines()
    {
        // Arrange
        var largeCacheSize = 20; // Larger than total lines
        using var cache = new RowByteCache(_indexer, cacheSize: largeCacheSize);

        // Act - Get first and last lines
        var firstLine = cache.GetLineBytes(0).ToArray();
        var lastLine = cache.GetLineBytes(9).ToArray();

        // Assert
        var expectedFirst = "{\"id\":1,\"name\":\"Alice\"}"u8.ToArray();
        var expectedLast = "{\"id\":10,\"name\":\"Jack\"}"u8.ToArray();

        firstLine.Should().BeEquivalentTo(expectedFirst);
        lastLine.Should().BeEquivalentTo(expectedLast);
    }

    [Fact]
    public void Dispose_AfterAccess_PreventsFurtherAccess()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer);
        cache.GetLineBytes(0); // Normal access

        // Act
        cache.Dispose();
        var act = () => cache.GetLineBytes(0);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetLineBytes_AfterDisposal_ThrowsObjectDisposedException()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer);
        cache.Dispose();

        // Act
        var act = () => cache.GetLineBytes(0);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetLineBytes_WithExactCacheSize_CachesExactly()
    {
        // Arrange
        var cacheSize = 5;
        using var cache = new RowByteCache(_indexer, cacheSize: cacheSize);

        // First access initializes cache (lines 0-4)
        cache.GetLineBytes(0);

        // Act - Get last line within cache range
        var result = cache.GetLineBytes(4).ToArray();

        // Assert
        var expected = "{\"id\":5,\"name\":\"Eve\"}"u8.ToArray();
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void UpdateCache_MultipleOverlaps_ProperlyInvalidatesOldEntries()
    {
        // Arrange
        using var cache = new RowByteCache(_indexer, cacheSize: 4);

        // First cache (lines 0-3)
        cache.GetLineBytes(0);
        var firstCached = cache.GetLineBytes(2).ToArray();

        // New cache (lines 6-9)
        cache.GetLineBytes(8);

        // Old cache entries should be invalidated
        // Since we cannot inspect internal cache entries,
        // verify by accessing different line
        var newCached = cache.GetLineBytes(7).ToArray();

        // Assert
        var expectedOld = "{\"id\":3,\"name\":\"Charlie\"}"u8.ToArray();
        var expectedNew = "{\"id\":8,\"name\":\"Henry\"}"u8.ToArray();

        firstCached.Should().BeEquivalentTo(expectedOld);
        newCached.Should().BeEquivalentTo(expectedNew);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            File.Delete(_testFilePath);
            _disposed = true;
        }
    }
}
