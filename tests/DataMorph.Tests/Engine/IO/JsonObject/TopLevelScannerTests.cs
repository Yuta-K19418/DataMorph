using AwesomeAssertions;
using DataMorph.Engine.IO.JsonObject;

namespace DataMorph.Tests.Engine.IO.JsonObject;

public sealed partial class TopLevelScannerTests : IDisposable
{
    private readonly string _testFilePath;

    public TopLevelScannerTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonobject_toplevelscanner_{Guid.NewGuid()}.json"
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
    public void Scan_WithNullFilePath_ThrowsArgumentException()
    {
        // Arrange — no setup required

        // Act
        var act = () => TopLevelScanner.Scan(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Scan_WithWhiteSpaceFilePath_ThrowsArgumentException()
    {
        // Arrange — no setup required

        // Act
        var act = () => TopLevelScanner.Scan("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
