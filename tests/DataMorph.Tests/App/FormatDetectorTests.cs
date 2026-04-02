using AwesomeAssertions;
using DataMorph.App;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.App;

public sealed class FormatDetectorTests : IDisposable
{
    private readonly string _csvFilePath;
    private readonly string _jsonlFilePath;
    private readonly string _unsupportedFilePath;
    private readonly string _emptyFilePath;
    private readonly string _nonExistentFilePath;

    public FormatDetectorTests()
    {
        _csvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _jsonlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        _unsupportedFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _emptyFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _nonExistentFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
    }

    public void Dispose()
    {
        foreach (var path in new[] { _csvFilePath, _jsonlFilePath, _unsupportedFilePath, _emptyFilePath })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Detect_WithNullOrEmptyPath_ReturnsFailure(string? path)
    {
        // Act
        var result = FormatDetector.Detect(path!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("path cannot be empty");
    }

    [Fact]
    public async Task Detect_WithCsvFile_ReturnsCsvFormat()
    {
        // Arrange
        await File.WriteAllTextAsync(_csvFilePath, "header\ndata");

        // Act
        var result = FormatDetector.Detect(_csvFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.Csv);
    }

    [Fact]
    public async Task Detect_WithJsonLinesFile_ReturnsJsonLinesFormat()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{}");

        // Act
        var result = FormatDetector.Detect(_jsonlFilePath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(DataFormat.JsonLines);
    }

    [Fact]
    public async Task Detect_WithUnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_unsupportedFilePath, "{}");

        // Act
        var result = FormatDetector.Detect(_unsupportedFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unsupported file format");
    }

    [Fact]
    public void Detect_WithNonExistentFile_ReturnsFailure()
    {
        // Act
        var result = FormatDetector.Detect(_nonExistentFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not exist");
    }

    [Fact]
    public async Task Detect_WithEmptyFile_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllBytesAsync(_emptyFilePath, []);

        // Act
        var result = FormatDetector.Detect(_emptyFilePath);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is empty");
    }
}
