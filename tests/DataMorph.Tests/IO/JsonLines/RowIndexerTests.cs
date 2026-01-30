using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.IO.JsonLines;

public sealed partial class RowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public RowIndexerTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonlinesRowIndexer_{Guid.NewGuid()}.jsonl"
        );
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Constructor_WithNullFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new RowIndexer(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhiteSpaceFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new RowIndexer("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidFilePath_SetsFilePathProperty()
    {
        // Arrange
        var expectedPath = "/path/to/file.jsonl";

        // Act
        var indexer = new RowIndexer(expectedPath);

        // Assert
        indexer.FilePath.Should().Be(expectedPath);
    }

    [Fact]
    public void TotalRows_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "{}");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        var totalRows = indexer.TotalRows;

        // Assert
        totalRows.Should().Be(0);
    }
}
