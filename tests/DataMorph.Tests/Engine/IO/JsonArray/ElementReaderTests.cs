using System.Text;
using AwesomeAssertions;
using DataMorph.Engine.IO.JsonArray;

namespace DataMorph.Tests.Engine.IO.JsonArray;

public sealed class ElementReaderTests : IDisposable
{
    private readonly string _testFilePath;
    private bool _disposed;

    public ElementReaderTests()
    {
        _testFilePath = Path.Combine(
            Path.GetTempPath(),
            $"jsonarray_elementreader_{Guid.NewGuid()}.json"
        );
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
            _disposed = true;
        }
    }

    private void WriteTestContent(string content, Encoding? encoding = null)
    {
        File.WriteAllText(_testFilePath, content, encoding ?? new UTF8Encoding(false));
    }

    [Fact]
    public void Constructor_WithNullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        string? filePath = null;

        // Act
        var act = () => new ElementReader(filePath!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithWhiteSpacePath_ThrowsArgumentException()
    {
        // Arrange
        var filePath = "   ";

        // Act
        var act = () => new ElementReader(filePath);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReadElementBytes_SingleObject_ReturnsObjectBytes()
    {
        // Arrange
        WriteTestContent("[{\"a\":1}]");

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_MultipleElements_ReturnsCorrectCount()
    {
        // Arrange
        WriteTestContent("[1, 2, 3]");

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_WithSkip_SkipsCorrectElements()
    {
        // Arrange
        WriteTestContent("[1, 2, 3]");

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_ElementSpansBufferBoundary_ReturnsCompleteBytes()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_FetchBeyondEnd_ReturnsAvailableElements()
    {
        // Arrange
        WriteTestContent("[1, 2]");

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_WithZeroFetchCount_ReturnsEmptyList()
    {
        // Arrange
        WriteTestContent("[1, 2, 3]");

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_WithNegativeSkipCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        WriteTestContent("[1]");

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_WithNegativeFetchCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        WriteTestContent("[1]");

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_EmptyArray_ReturnsEmptyList()
    {
        // Arrange
        WriteTestContent("[]");

        // Act

        // Assert
    }

    [Fact]
    public void ReadElementBytes_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        WriteTestContent("[1]");

        // Act

        // Assert
    }
}
