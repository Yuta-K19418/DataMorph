using System.Text;
using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.Engine.IO.JsonLines;

public sealed class RowReaderTests : IDisposable
{
    private readonly string _testFilePath;
    private bool _disposed;

    public RowReaderTests()
    {
        _testFilePath = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            File.Delete(_testFilePath);
            _disposed = true;
        }
    }

    private void WriteTestContent(string content)
    {
        File.WriteAllText(_testFilePath, content);
    }

    [Fact]
    public void Constructor_WithNullFilePath_ThrowsArgumentNullException()
    {
        // Arrange
        string? filePath = null;

        // Act
        var act = () => new RowReader(filePath!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReadLineBytes_WithSingleJsonLine_ReturnsOneLine()
    {
        // Arrange
        WriteTestContent("{\"id\":1}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLineBytes(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        lines.Should().ContainSingle();
        var lineString = Encoding.UTF8.GetString(lines[0].Span);
        lineString.Should().Be("{\"id\":1}");
    }

    [Fact]
    public void ReadLineBytes_WithMultipleJsonLines_ReturnsAllLines()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLineBytes(byteOffset: 0, linesToSkip: 0, linesToRead: 3);

        // Assert
        lines.Should().HaveCount(3);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"a\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"b\":2}");
        Encoding.UTF8.GetString(lines[2].Span).Should().Be("{\"c\":3}");
    }

    [Fact]
    public void ReadLineBytes_WithLinesToSkip_SkipsCorrectly()
    {
        // Arrange
        WriteTestContent("{\"skip\":0}\n{\"read\":1}\n{\"read\":2}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLineBytes(byteOffset: 0, linesToSkip: 1, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"read\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"read\":2}");
    }

    [Fact]
    public void ReadLineBytes_WithLinesToRead_LimitsCorrectly()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n{\"b\":2}\n{\"c\":3}\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLineBytes(byteOffset: 0, linesToSkip: 0, linesToRead: 2);

        // Assert
        lines.Should().HaveCount(2);
        Encoding.UTF8.GetString(lines[0].Span).Should().Be("{\"a\":1}");
        Encoding.UTF8.GetString(lines[1].Span).Should().Be("{\"b\":2}");
    }

    [Fact]
    public void Constructor_WithEmptyFile_ThrowsInvalidOperationException()
    {
        // Arrange
        WriteTestContent("");

        // Act
        var act = () => new RowReader(_testFilePath);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReadLineBytes_WithInvalidJsonLine_ThrowsInvalidDataException()
    {
        // Arrange
        WriteTestContent("invalid json\n");
        using var reader = new RowReader(_testFilePath);

        // Act
        var act = () => reader.ReadLineBytes(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadLineBytes_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        WriteTestContent("{\"test\":1}\n");
        var reader = new RowReader(_testFilePath);
        reader.Dispose();

        // Act
        var act = () => reader.ReadLineBytes(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ReadLineBytes_WithOffsetBeyondEOF_ReturnsEmptyList()
    {
        // Arrange
        WriteTestContent("{\"a\":1}\n");
        using var reader = new RowReader(_testFilePath);
        long largeOffset = 1000; // beyond file size

        // Act
        var lines = reader.ReadLineBytes(byteOffset: largeOffset, linesToSkip: 0, linesToRead: 1);

        // Assert
        lines.Should().BeEmpty();
    }

    [Fact]
    public void ReadLineBytes_WithIncompleteLineAtEOF_ReturnsEmptyList()
    {
        // Arrange
        // Incomplete JSON line without newline at EOF
        WriteTestContent("{\"incomplete\":");
        using var reader = new RowReader(_testFilePath);

        // Act
        var lines = reader.ReadLineBytes(byteOffset: 0, linesToSkip: 0, linesToRead: 1);

        // Assert
        // According to the current implementation, incomplete lines are not returned.
        lines.Should().BeEmpty();
    }
}
