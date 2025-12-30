using DataMorph.Engine.IO;
using FluentAssertions;

namespace DataMorph.Tests.IO;

public sealed class RowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public RowIndexerTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"rowIndexer_{Guid.NewGuid()}.txt");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Build_WithSingleLine_ReturnsIndexWithOneRow()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "single line");
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act
        var result = RowIndexer.Build(mmapService);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(1);
        indexer[0].Should().Be(0);
    }

    [Fact]
    public void Build_WithMultipleLinesLF_IndexesAllRows()
    {
        // Arrange
        var content = "line1\nline2\nline3";
        File.WriteAllText(_testFilePath, content);
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act
        var result = RowIndexer.Build(mmapService);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(3);
        indexer[0].Should().Be(0);      // "line1"
        indexer[1].Should().Be(6);      // "line2"
        indexer[2].Should().Be(12);     // "line3"
    }

    [Fact]
    public void Build_WithMultipleLinesCRLF_IndexesAllRows()
    {
        // Arrange
        var content = "line1\r\nline2\r\nline3";
        File.WriteAllText(_testFilePath, content);
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act
        var result = RowIndexer.Build(mmapService);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(3);
        indexer[0].Should().Be(0);      // "line1"
        indexer[1].Should().Be(7);      // "line2"
        indexer[2].Should().Be(14);     // "line3"
    }

    [Fact]
    public void Build_WithMixedLineEndings_IndexesAllRows()
    {
        // Arrange
        var content = "line1\nline2\r\nline3\rline4";
        File.WriteAllText(_testFilePath, content);
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act
        var result = RowIndexer.Build(mmapService);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(4);
        indexer[0].Should().Be(0);      // "line1"
        indexer[1].Should().Be(6);      // "line2"
        indexer[2].Should().Be(13);     // "line3"
        indexer[3].Should().Be(19);     // "line4"
    }

    [Fact]
    public void Build_WithTrailingNewline_IndexesCorrectly()
    {
        // Arrange
        var content = "line1\nline2\n";
        File.WriteAllText(_testFilePath, content);
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act
        var result = RowIndexer.Build(mmapService);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(3);
        indexer[0].Should().Be(0);
        indexer[1].Should().Be(6);
        indexer[2].Should().Be(12);
    }

    [Fact]
    public void Build_WithLargeFile_ProcessesInChunks()
    {
        // Arrange: Create a file larger than default chunk size (1MB)
        // Use explicit LF to avoid platform-dependent CRLF issues with chunk boundaries
        const int lineCount = 100_000;
        var content = string.Join("\n", Enumerable.Range(0, lineCount).Select(i => $"Line {i:D6}")) + "\n";
        File.WriteAllText(_testFilePath, content);

        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act
        var result = RowIndexer.Build(mmapService, chunkSize: 4096);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(lineCount + 1); // +1 for the empty line after last newline
    }

    [Fact]
    public void Build_WithSmallChunkSize_StillIndexesCorrectly()
    {
        // Arrange
        var content = "line1\nline2\nline3";
        File.WriteAllText(_testFilePath, content);
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act - Use very small chunk size to force multiple chunks
        var result = RowIndexer.Build(mmapService, chunkSize: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(3);
        indexer[0].Should().Be(0);
        indexer[1].Should().Be(6);
        indexer[2].Should().Be(12);
    }

    [Fact]
    public void Indexer_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "line1\nline2");
        using var mmapService = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmapService).Value;

        // Act & Assert
        var act = () => _ = indexer[-1];
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_WithIndexEqualToRowCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "line1\nline2");
        using var mmapService = MmapService.Open(_testFilePath).Value;
        using var indexer = RowIndexer.Build(mmapService).Value;

        // Act & Assert
        var act = () => _ = indexer[indexer.RowCount];
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "line1\nline2");
        using var mmapService = MmapService.Open(_testFilePath).Value;
        var indexer = RowIndexer.Build(mmapService).Value;
        indexer.Dispose();

        // Act & Assert
        var act = () => _ = indexer[0];
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Build_WithNullMmapService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => RowIndexer.Build(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_WithNegativeChunkSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act & Assert
        var act = () => RowIndexer.Build(mmapService, chunkSize: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Build_WithZeroChunkSize_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act & Assert
        var act = () => RowIndexer.Build(mmapService, chunkSize: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Build_WithOnlyNewlines_IndexesCorrectly()
    {
        // Arrange
        var content = "\n\n\n";
        File.WriteAllText(_testFilePath, content);
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act
        var result = RowIndexer.Build(mmapService);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(4); // empty, empty, empty, empty
        indexer[0].Should().Be(0);
        indexer[1].Should().Be(1);
        indexer[2].Should().Be(2);
        indexer[3].Should().Be(3);
    }

    [Fact]
    public void Build_WithUnicodeContent_IndexesByteOffsetsCorrectly()
    {
        // Arrange
        var content = "日本語\n漢字\n"; // Multi-byte UTF-8 characters
        File.WriteAllText(_testFilePath, content);
        using var mmapService = MmapService.Open(_testFilePath).Value;

        // Act
        var result = RowIndexer.Build(mmapService);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var indexer = result.Value;
        indexer.RowCount.Should().Be(3);

        // UTF-8: "日本語" = 9 bytes, "\n" = 1 byte = 10 bytes
        // UTF-8: "漢字" = 6 bytes, "\n" = 1 byte = 7 bytes
        indexer[0].Should().Be(0);
        indexer[1].Should().Be(10);  // After "日本語\n"
        indexer[2].Should().Be(17);  // After "日本語\n漢字\n"
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "test");
        using var mmapService = MmapService.Open(_testFilePath).Value;
        var indexer = RowIndexer.Build(mmapService).Value;

        // Act & Assert
        var act = () =>
        {
            indexer.Dispose();
            indexer.Dispose();
            indexer.Dispose();
        };
        act.Should().NotThrow();
    }
}
