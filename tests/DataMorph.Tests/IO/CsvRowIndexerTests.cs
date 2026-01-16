using AwesomeAssertions;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.IO;

public sealed partial class CsvRowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public CsvRowIndexerTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"csvRowIndexer_{Guid.NewGuid()}.csv"
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
        var act = () => new CsvRowIndexer(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new CsvRowIndexer(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new CsvRowIndexer("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TotalRows_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2");
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act & Assert
        indexer.TotalRows.Should().Be(0);
    }
}
