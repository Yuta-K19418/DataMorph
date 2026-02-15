using System.Text;
using AwesomeAssertions;

namespace DataMorph.Engine.IO.JsonLines.Tests;

public sealed class JsonLinesScannerTests
{
    [Fact]
    public void FindNextLineLength_EmptySpan_ReturnsFalseAndZero()
    {
        // Arrange
        var span = ReadOnlySpan<byte>.Empty;

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeFalse();
        bytesConsumed.Should().Be(0);
    }

    [Fact]
    public void FindNextLineLength_SingleLineWithoutQuotes_ReturnsFullLength()
    {
        // Arrange
        var line = Encoding.UTF8.GetBytes("{\"name\":\"John\",\"age\":30}\n");
        var span = new ReadOnlySpan<byte>(line);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(line.Length);
    }

    [Fact]
    public void FindNextLineLength_TwoLinesWithoutQuotes_ReturnsFirstLineLength()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"name\":\"John\"}\n{\"name\":\"Jane\"}\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        // First line is 15 bytes + 1 byte for newline = 16
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(16);
    }

    [Fact]
    public void FindNextLineLength_NewlineInsideQuotes_NotConsideredLineEnd()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"name\":\"John\\nDoe\",\"age\":30}\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        // The newline inside the string is escaped, so it's part of the string
        // The actual line ends at the newline outside quotes
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_EscapedQuoteInsideString_DoesNotToggleQuoteState()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"name\":\"John\\\"Doe\",\"age\":30}\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        // The escaped quote should not break the string, so the newline outside quotes ends the line
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_MultipleBackslashesBeforeQuote_HandlesCorrectly()
    {
        // Arrange
        // \"\"\" -> \\\"\\\"\\\" in bytes
        var input = Encoding.UTF8.GetBytes("{\"name\":\"John\\\\\\\"Doe\",\"age\":30}\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        // Should correctly parse the line
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_NoNewline_ReturnsFullSpanLength()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"name\":\"John\",\"age\":30}");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeFalse();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_NewlineOutsideQuotes_EndsLine()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"name\":\"John\"}\n{\"age\":30}");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        // First line is 15 bytes + 1 byte for newline = 16
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(16);
    }

    [Fact]
    public void FindNextLineLength_ComplexNestedQuotes_HandlesCorrectly()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"name\":\"John \\\"The\\\" Doe\",\"age\":30}\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_EscapedBackslashInsideString_HandlesCorrectly()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"path\":\"C:\\\\Users\\\\John\",\"age\":30}\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        // Should correctly parse the line
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Theory]
    [InlineData("{\"name\":\"John\"}\n", 16)]
    [InlineData("{\"name\":\"John\"}\n{\"age\":30}\n", 16)]
    [InlineData("{\"name\":\"John\\nDoe\"}\n", 21)]
    [InlineData("{\"name\":\"John\\\"Doe\"}\n", 21)]
    public void FindNextLineLength_VariousInputs_ReturnsExpectedLength(
        string inputString,
        int expectedLength
    )
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes(inputString);
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(expectedLength);
    }

    [Fact]
    public void FindNextLineLength_QuotesSpanningBuffers_HandlesStateCorrectly()
    {
        // Arrange: First buffer with opening quote only
        var buffer1 = Encoding.UTF8.GetBytes("\"long value");
        var scanner = new JsonLinesScanner();

        // Act (First buffer)
        var (lineCompleted1, bytesConsumed1) = scanner.FindNextLineLength(buffer1);

        // Assert (First buffer)
        lineCompleted1.Should().BeFalse();
        bytesConsumed1.Should().Be(buffer1.Length);

        // Arrange: Second buffer with continuation and closing quote + newline
        var buffer2 = Encoding.UTF8.GetBytes(" continued\"\n");

        // Act (Second buffer)
        var (lineCompleted2, bytesConsumed2) = scanner.FindNextLineLength(buffer2);

        // Assert (Second buffer)
        lineCompleted2.Should().BeTrue();
        bytesConsumed2.Should().Be(buffer2.Length);
    }

    [Fact]
    public void FindNextLineLength_EscapedBackslashAtBufferBoundary_HandlesCorrectly()
    {
        // Arrange: First buffer with partial escape sequence
        var buffer1 = Encoding.UTF8.GetBytes("{\"path\":\"C:\\\\");
        var scanner = new JsonLinesScanner();

        // Act (First buffer)
        var (lineCompleted1, bytesConsumed1) = scanner.FindNextLineLength(buffer1);

        // Assert (First buffer)
        lineCompleted1.Should().BeFalse();
        bytesConsumed1.Should().Be(buffer1.Length);

        // Arrange: Second buffer with remainder of escape sequence
        var buffer2 = Encoding.UTF8.GetBytes("Users\\\\John\"}\n");

        // Act (Second buffer)
        var (lineCompleted2, bytesConsumed2) = scanner.FindNextLineLength(buffer2);

        // Assert (Second buffer)
        lineCompleted2.Should().BeTrue();
        bytesConsumed2.Should().Be(buffer2.Length);
    }

    [Theory]
    [InlineData("{}", 2)]
    [InlineData("[]", 2)]
    [InlineData("{\"key\":null}", 12)]
    [InlineData("[1,2,3]", 7)]
    public void FindNextLineLength_SimpleJsonStructures_ReturnsCorrectLength(
        string json,
        int expectedLength
    )
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes(json + "\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(expectedLength + 1); // +1 for newline
    }

    [Fact]
    public void FindNextLineLength_InvalidEscapeSequence_HandlesGracefully()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"text\":\"\\x\"}\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_NestedObjectWithString_HandlesCorrectly()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"obj\":{\"key\":\"value\\n\"}}\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_ArrayWithStrings_HandlesCorrectly()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("[\"line1\\n\",\"line2\"]\n");
        var span = new ReadOnlySpan<byte>(input);

        // Act
        var scanner = new JsonLinesScanner();
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_ConsecutiveBuffers_StatePreserved()
    {
        // Arrange
        var buffer1 = Encoding.UTF8.GetBytes("{\"text\":\"partial");
        var buffer2 = Encoding.UTF8.GetBytes(" value\"}\n");

        var scanner = new JsonLinesScanner();

        // Act (first buffer)
        var (lineCompleted1, bytesConsumed1) = scanner.FindNextLineLength(buffer1);

        // Assert (first buffer)
        lineCompleted1.Should().BeFalse();
        bytesConsumed1.Should().Be(buffer1.Length);

        // Act (second buffer)
        var (lineCompleted2, bytesConsumed2) = scanner.FindNextLineLength(buffer2);

        // Assert (second buffer)
        lineCompleted2.Should().BeTrue();
        bytesConsumed2.Should().Be(buffer2.Length);
    }

    [Fact]
    public void FindNextLineLength_VeryLongLineSpanningBuffers_HandlesCorrectly()
    {
        // Arrange: Create a JSON line longer than the scanner's buffer size
        const int bufferSize = 1024; // Simulate scanner buffer size
        var longValue = new string('a', bufferSize * 3); // 3x buffer size
        var buffer1 = Encoding.UTF8.GetBytes($"{{\"id\":1,\"data\":\"{longValue}");
        var buffer2 = Encoding.UTF8.GetBytes($"\"}}\n");
        var scanner = new JsonLinesScanner();

        // Act: Process first buffer (partial data)
        var (lineCompleted1, consumed1) = scanner.FindNextLineLength(buffer1);

        // Assert: Verify no line end found in first buffer
        lineCompleted1.Should().BeFalse();
        consumed1.Should().Be(buffer1.Length);

        // Act: Process second buffer (remaining data with newline)
        var (lineCompleted2, consumed2) = scanner.FindNextLineLength(buffer2);

        // Assert: Verify line end found and total bytes consumed
        lineCompleted2.Should().BeTrue();
        consumed2.Should().Be(buffer2.Length);
        (consumed1 + consumed2)
            .Should()
            .Be(Encoding.UTF8.GetByteCount($"{{\"id\":1,\"data\":\"{longValue}\"}}") + 1);
    }

    [Fact]
    public void FindNextLineLength_LongLineWithEscapedCharacters_HandlesCorrectly()
    {
        // Arrange: Create JSON with escape sequence spanning buffers
        var buffer1 = Encoding.UTF8.GetBytes("{\"data\":\"value\\");
        var buffer2 = Encoding.UTF8.GetBytes("n\"}\n");
        var scanner = new JsonLinesScanner();

        // Act: Process first buffer (ends with escape character)
        var (lineCompleted1, consumed1) = scanner.FindNextLineLength(buffer1);

        // Assert: Verify no line end found
        lineCompleted1.Should().BeFalse();
        consumed1.Should().Be(buffer1.Length);

        // Act: Process second buffer (completes escape sequence)
        var (lineCompleted2, consumed2) = scanner.FindNextLineLength(buffer2);

        // Assert: Verify line end found
        lineCompleted2.Should().BeTrue();
        consumed2.Should().Be(buffer2.Length);
    }

    [Fact]
    public void FindNextLineLength_EmptyJsonObjectWithNewline_ReturnsCompleteLine()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{}\n");
        var span = new ReadOnlySpan<byte>(input);
        var scanner = new JsonLinesScanner();

        // Act
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(3);
    }

    [Fact]
    public void FindNextLineLength_LineEndingWithCRLF_ReturnsCorrectLength()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"name\":\"John\"}\r\n");
        var span = new ReadOnlySpan<byte>(input);
        var scanner = new JsonLinesScanner();

        // Act
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_EscapedQuoteAtBufferBoundary_HandlesCorrectly()
    {
        // Arrange
        var buffer1 = Encoding.UTF8.GetBytes("{\"text\":\"value\\");
        var buffer2 = Encoding.UTF8.GetBytes("\"more text\"}\n");
        var scanner = new JsonLinesScanner();

        // Act (First buffer)
        var (lineCompleted1, consumed1) = scanner.FindNextLineLength(buffer1);

        // Assert (First buffer)
        lineCompleted1.Should().BeFalse();
        consumed1.Should().Be(buffer1.Length);

        // Act (Second buffer)
        var (lineCompleted2, consumed2) = scanner.FindNextLineLength(buffer2);

        // Assert (Second buffer)
        lineCompleted2.Should().BeTrue();
        consumed2.Should().Be(buffer2.Length);
    }

    [Theory]
    [InlineData("\n", 1)] // Empty line
    [InlineData("  \n", 3)] // Whitespace line
    [InlineData("{\"a\":1}\n\n{\"b\":2}\n", 8)] // Empty line between two valid lines
    public void FindNextLineLength_WithEmptyOrWhitespaceLines_FindsNextNewline(
        string inputString,
        int expectedBytesConsumed
    )
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes(inputString);
        var span = new ReadOnlySpan<byte>(input);
        var scanner = new JsonLinesScanner();

        // Act
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(expectedBytesConsumed);
    }

    [Fact]
    public void FindNextLineLength_MultipleConsecutiveBackslashes_HandlesCorrectly()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"path\":\"C:\\\\\\\\\\\\\"}\n");
        var span = new ReadOnlySpan<byte>(input);
        var scanner = new JsonLinesScanner();

        // Act
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Fact]
    public void FindNextLineLength_QuoteClosedAtBufferBoundary_HandlesCorrectly()
    {
        // Arrange
        var buffer1 = Encoding.UTF8.GetBytes("{\"text\":\"partial");
        var buffer2 = Encoding.UTF8.GetBytes("\"}\n");
        var scanner = new JsonLinesScanner();

        // Act (First buffer)
        var (lineCompleted1, consumed1) = scanner.FindNextLineLength(buffer1);

        // Assert (First buffer)
        lineCompleted1.Should().BeFalse();
        consumed1.Should().Be(buffer1.Length);

        // Act (Second buffer)
        var (lineCompleted2, consumed2) = scanner.FindNextLineLength(buffer2);

        // Assert (Second buffer)
        lineCompleted2.Should().BeTrue();
        consumed2.Should().Be(buffer2.Length);
    }

    [Fact]
    public void FindNextLineLength_ComplexEscapeSequence_HandlesCorrectly()
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes("{\"text\":\"\\\"\\\\\\/\\b\\f\\n\\r\\t\\u0020\"}\n");
        var span = new ReadOnlySpan<byte>(input);
        var scanner = new JsonLinesScanner();

        // Act
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().BeTrue();
        bytesConsumed.Should().Be(input.Length);
    }

    [Theory]
    [InlineData("{\"a\":1}\n", true, 8)]
    [InlineData("{\"a\":1}\n{\"b\":2}\n", true, 8)]
    [InlineData("{\"a\":1}", false, 7)]
    [InlineData("{\"a\":\"line1\\nline2\"}\n", true, 21)]
    public void FindNextLineLength_VariousJsonLines_ReturnsExpectedResult(
        string inputString,
        bool expectedLineCompleted,
        int expectedBytesConsumed
    )
    {
        // Arrange
        var input = Encoding.UTF8.GetBytes(inputString);
        var span = new ReadOnlySpan<byte>(input);
        var scanner = new JsonLinesScanner();

        // Act
        var (lineCompleted, bytesConsumed) = scanner.FindNextLineLength(span);

        // Assert
        lineCompleted.Should().Be(expectedLineCompleted);
        bytesConsumed.Should().Be(expectedBytesConsumed);
    }
}
