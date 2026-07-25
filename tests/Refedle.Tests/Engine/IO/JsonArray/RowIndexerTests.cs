using AwesomeAssertions;
using DataMorph.Engine.IO.JsonArray;

namespace DataMorph.Tests.Engine.IO.JsonArray;

public sealed partial class RowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public RowIndexerTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarrayRowIndexer_{Guid.NewGuid()}.json"
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
        // Arrange — no setup required

        // Act
        var act = () => new RowIndexer(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhiteSpaceFilePath_ThrowsArgumentException()
    {
        // Arrange — no setup required

        // Act
        var act = () => new RowIndexer("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidFilePath_SetsFilePathProperty()
    {
        // Arrange
        var expectedPath = "/path/to/file.json";

        // Act
        var indexer = new RowIndexer(expectedPath);

        // Assert
        indexer.FilePath.Should().Be(expectedPath);
    }

    [Fact]
    public void TotalRows_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "[]");
        var indexer = new RowIndexer(_testFilePath);

        // Act
        var totalRows = indexer.TotalRows;

        // Assert
        totalRows.Should().Be(0);
    }
}
