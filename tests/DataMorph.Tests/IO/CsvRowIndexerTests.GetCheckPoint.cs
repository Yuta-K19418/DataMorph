using AwesomeAssertions;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.IO;

public sealed partial class CsvRowIndexerTests
{
    [Fact]
    public void GetCheckPoint_WithRowLessThanCheckpoint_ReturnsFirstCheckpoint()
    {
        // Arrange: Fixed-length rows "v001,d001\n" = 10 bytes each
        // Header "col1,col2\n" = 10 bytes
        var lines = new List<string> { "col1,col2" };
        lines.AddRange(Enumerable.Range(1, 500).Select(i => $"v{i:D3},d{i:D3}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines) + "\n");

        var indexer = new CsvRowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(250);

        // Assert
        byteOffset.Should().Be(0); // First checkpoint at file start
        rowOffset.Should().Be(250);
    }

    [Fact]
    public void GetCheckPoint_WithExactCheckpointRow_ReturnsExactCheckpoint()
    {
        // Arrange: Fixed-length rows "v0001,d0001\n" = 12 bytes each
        // Header "col01,col02\n" = 12 bytes
        var lines = new List<string> { "col01,col02" };
        lines.AddRange(Enumerable.Range(1, 1500).Select(i => $"v{i:D4},d{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines) + "\n");

        var indexer = new CsvRowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(1000);

        // Assert
        // Checkpoint 1000: After header + 999 data rows = 12 + (999 * 12) = 12 + 11988 = 12000 bytes
        byteOffset.Should().Be(12000);
        rowOffset.Should().Be(0);
    }

    [Fact]
    public void GetCheckPoint_WithRowBeyondLastCheckpoint_ReturnsLastCheckpoint()
    {
        // Arrange: Fixed-length rows "v0001,d0001\n" = 12 bytes each
        // Header "col01,col02\n" = 12 bytes
        var lines = new List<string> { "col01,col02" };
        lines.AddRange(Enumerable.Range(1, 1200).Select(i => $"v{i:D4},d{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines) + "\n");

        var indexer = new CsvRowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Act
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(1150);

        // Assert
        // Checkpoint 1000: After header + 999 data rows = 12 + (999 * 12) = 12 + 11988 = 12000 bytes
        // Row 1150 uses the same checkpoint 1000
        byteOffset.Should().Be(12000);
        rowOffset.Should().Be(150);
    }

    [Fact]
    public void GetCheckPoint_WithRowBeyondTotalRows_ReturnsLastCheckpointWithCorrectOffset()
    {
        // Arrange: File with 2000 rows (checkpoints at 0, 1000, 2000)
        // Fixed-length rows "v0001,d0001\n" = 12 bytes each
        // Header "col01,col02\n" = 12 bytes
        var lines = new List<string> { "col01,col02" };
        lines.AddRange(Enumerable.Range(1, 2000).Select(i => $"v{i:D4},d{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines) + "\n");

        var indexer = new CsvRowIndexer(_testFilePath);
        indexer.BuildIndex();

        // Verify TotalRows is 2001 (header + 2000 data rows)
        indexer.TotalRows.Should().Be(2001);

        // Act: Request row 5000, which is far beyond TotalRows
        var (byteOffset, rowOffset) = indexer.GetCheckPoint(5000);

        // Assert: Returns last checkpoint (2000)
        // idealCheckPointIndex = 5000 / 1000 = 5
        // actualCheckPointIndex = Math.Min(5, 2) = 2 (checkpoints: 0, 1000, 2000)
        // actualCheckPointRow = 2 * 1000 = 2000
        // rowOffset = 5000 - 2000 = 3000
        // Checkpoint 2000: After header + 1999 data rows = 12 + (1999 * 12) = 12 + 23988 = 24000 bytes
        byteOffset.Should().Be(24000); // Last checkpoint at row 2000
        rowOffset.Should().Be(3000); // Need to skip 3000 rows from checkpoint 2000
    }
}
