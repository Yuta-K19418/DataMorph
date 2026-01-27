using AwesomeAssertions;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.IO;

public sealed partial class CsvDataRowIndexerTests : IDisposable
{
    private readonly string _testFilePath;

    public CsvDataRowIndexerTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"csvRowIndexer_{Guid.NewGuid()}.csv");
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
        var act = () => new CsvDataRowIndexer(null!);
        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("Value cannot be null.*")
            .WithParameterName("filePath");
    }

    [Fact]
    public void Constructor_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new CsvDataRowIndexer(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithWhitespaceFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new CsvDataRowIndexer("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TotalRows_BeforeBuildIndex_ReturnsZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2");
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act & Assert
        indexer.TotalRows.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithNonExistentFile_DoesNotThrow()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.csv");

        // Act & Assert (constructor should not throw for non-existent file)
        var indexer = new CsvDataRowIndexer(nonExistentPath);
        indexer.FilePath.Should().Be(nonExistentPath);
        indexer.TotalRows.Should().Be(0);
    }
}
