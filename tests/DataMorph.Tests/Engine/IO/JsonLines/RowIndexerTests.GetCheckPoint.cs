using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.Engine.IO.JsonLines;

public sealed partial class RowIndexerTests
{
    [Fact]
    public void GetCheckPoint_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{\"id\": 1}");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        var result = indexer.GetCheckPoint(0);

        // Assert
        result.byteOffset.Should().Be(0); // _checkpoints is initialized with [0]
        result.rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_AfterBuildIndex_ReturnsCorrectCheckpoints()
    {
        // Arrange
        var content = "{\"id\": 1}\n{\"id\": 2}\n{\"id\": 3}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act & Assert
        var (byteOffset0, rowOffset0) = indexer.GetCheckPoint(0);
        byteOffset0.Should().Be(0); // First checkpoint is at start of file
        rowOffset0.Should().Be(0);

        var (byteOffset1, rowOffset1) = indexer.GetCheckPoint(1);
        byteOffset1.Should().Be(0);
        rowOffset1.Should().Be(1);

        var (byteOffset2, rowOffset2) = indexer.GetCheckPoint(2);
        byteOffset2.Should().Be(0);
        rowOffset2.Should().Be(2);
    }

    [Fact]
    public void GetCheckPoint_WithCheckpointInterval_ReturnsCorrectOffsets()
    {
        // Arrange: Create file with more than CheckPointInterval (1000) rows
        var lines = Enumerable.Range(0, 1500).Select(i => $"{{\"id\": {i}}}");
        var content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act & Assert
        var (byteOffset500, rowOffset500) = indexer.GetCheckPoint(500);
        byteOffset500.Should().Be(0); // Before first checkpoint
        rowOffset500.Should().Be(500);

        var (byteOffset999, rowOffset999) = indexer.GetCheckPoint(999);
        byteOffset999.Should().Be(0);
        rowOffset999.Should().Be(999);

        var (byteOffset1000, rowOffset1000) = indexer.GetCheckPoint(1000);
        byteOffset1000.Should().Be(11890); // Checkpoint should be at 1000th row boundary
        rowOffset1000.Should().Be(0);

        var (byteOffset1499, rowOffset1499) = indexer.GetCheckPoint(1499);
        byteOffset1499.Should().Be(11890); // Checkpoint at 1000 rows
        rowOffset1499.Should().Be(499); // 1499 - 1000 = 499 rows from checkpoint
    }

    [Fact]
    public void GetCheckPoint_RequestBeyondTotalRows_ClampsToLastCheckpoint()
    {
        // Arrange
        var content = "{\"id\": 1}\n{\"id\": 2}\n{\"id\": 3}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(100);

        // Assert
        byteOffset.Should().Be(0); // Clamped to last checkpoint
        rowOffset.Should().Be(100); // But rowOffset is still 100 (will be handled by caller)
    }

    [Fact]
    public void GetCheckPoint_WithEmptyFile_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(0);

        // Assert
        byteOffset.Should().Be(0); // _checkpoints is initialized with [0]
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithSingleRow_ReturnsCorrectOffset()
    {
        // Arrange
        var content = "{\"id\": 1}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(0);

        // Assert
        byteOffset.Should().Be(0);
        rowOffset.Should().Be(0);
    }

    [Fact]
    public async Task GetCheckPoint_WhileIndexingInBackground_ShouldBeThreadSafe()
    {
        // Arrange: Create a large file (2500 rows) to test concurrent access during indexing
        var lines = Enumerable.Range(0, 2500).Select(i => $"{{\"id\": {i}}}");
        var content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act: Start indexing in background and access checkpoints concurrently
        var buildTask = Task.Run(() => indexer.BuildIndex());

        // Assert: Checkpoints should be accessible even during indexing
        var (byteOffset0, rowOffset0) = indexer.GetCheckPoint(0);
        var (byteOffset500, rowOffset500) = indexer.GetCheckPoint(500);
        // During indexing, checkpoint may not be available yet
        // But method should not crash and return valid values

        // Wait for indexing to complete
        await buildTask;

        // Assert
        byteOffset0.Should().Be(0);
        rowOffset0.Should().Be(0);
        byteOffset500.Should().Be(0);
        rowOffset500.Should().Be(500);
        // After indexing, checkpoints should be properly set
        var (finalByteOffset1000, finalRowOffset1000) = indexer.GetCheckPoint(1000);
        finalByteOffset1000.Should().Be(11890);
        finalRowOffset1000.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithExactCheckpointRow_ReturnsZeroRowOffset()
    {
        // Arrange: Create file with checkpoint interval rows + 1 to have exact checkpoint row
        var lines = Enumerable.Range(0, 1001).Select(i => $"{{\"id\": {i}}}");
        var content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(1000);

        // Assert
        byteOffset.Should().Be(11890); // Checkpoint should be at 1000th row boundary
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithLargeFile_ReturnsValidCheckpoints()
    {
        // Arrange: Create file with 10,000 rows to ensure multiple checkpoints
        var lines = Enumerable.Range(0, 10_000).Select(i => $"{{\"id\": {i}}}");
        var content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Test specific positions
        var (byteOffset0, rowOffset0) = indexer.GetCheckPoint(0);
        byteOffset0.Should().Be(0);
        rowOffset0.Should().Be(0);

        var (byteOffset999, rowOffset999) = indexer.GetCheckPoint(999);
        byteOffset999.Should().Be(0);
        rowOffset999.Should().Be(999);

        var (byteOffset1000, rowOffset1000) = indexer.GetCheckPoint(1000);
        byteOffset1000.Should().Be(11890); // Checkpoint at 1000 rows
        rowOffset1000.Should().Be(0);

        var (byteOffset9999, rowOffset9999) = indexer.GetCheckPoint(9999);
        byteOffset9999.Should().Be(115890); // Checkpoint at 9000 rows
        rowOffset9999.Should().Be(999); // 999 rows from checkpoint
    }
}
