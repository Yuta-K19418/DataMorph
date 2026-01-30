using AwesomeAssertions;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.IO.JsonLines;

public sealed partial class RowIndexerTests
{
    [Fact]
    public void BuildIndex_WithMultipleJsonObjectsLF_IndexesAllRows()
    {
        // Arrange
        var content = """
            {"id": 1, "name": "Alice"}
            {"id": 2, "name": "Bob"}
            {"id": 3, "name": "Charlie"}
            """;
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithMultipleJsonObjectsCRLF_IndexesAllRows()
    {
        // Arrange
        var content =
            "{\"id\": 1, \"name\": \"Alice\"}\r\n{\"id\": 2, \"name\": \"Bob\"}\r\n{\"id\": 3, \"name\": \"Charlie\"}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithMixedLineEndings_IndexesAllRows()
    {
        // Arrange
        var content = "{\"id\": 1}\n{\"id\": 2}\r\n{\"id\": 3}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithTrailingNewline_IndexesCorrectly()
    {
        // Arrange
        var content = "{\"id\": 1}\n{\"id\": 2}\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // 2 data rows (trailing newline doesn't create a new row)
    }

    [Fact]
    public void BuildIndex_WithoutTrailingNewline_CountsLastRow()
    {
        // Arrange
        var content = "{\"id\": 1}\n{\"id\": 2}\n{\"id\": 3}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithComplexJson_IndexesCorrectly()
    {
        // Arrange
        var content = """
            {"id": 1, "nested": {"name": "Alice"}, "array": [1, 2, 3]}
            {"id": 2, "nested": {"name": "Bob"}, "array": [4, 5, 6]}
            {"id": 3, "nested": {"name": "Charlie"}, "array": [7, 8, 9]}
            """;
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithJsonContainingNewlineEscapes_HandlesCorrectly()
    {
        // Arrange
        var content =
            "{\"text\": \"line1\\\\nline2\\\\nline3\"}\n{\"text\": \"another\\\\nline\"}\n{\"text\": \"escaped\\\\nstring\"}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithUnicodeContent_IndexesCorrectly()
    {
        // Arrange
        var content = """
            {"name": "日本語", "id": 1}
            {"name": "한국어", "id": 2}
            {"name": "हिन्दी", "id": 3}
            """;
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3);
    }

    [Fact]
    public void BuildIndex_WithEmptyLines_CountsEmptyRows()
    {
        // Arrange
        var content = "\n\n{\"id\": 1}\n\n\n{\"id\": 2}\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(6); // 2 empty + 1 data + 3 empty + 1 data
    }

    [Fact]
    public void BuildIndex_WithWhitespaceOnlyLines_CountsAsRows()
    {
        // Arrange
        var content = "   \n\t\n{\"id\": 1}\n  \n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(4); // 2 whitespace + 1 data + 1 whitespace
    }

    [Fact]
    public void BuildIndex_WithEmptyFile_SetsTotalRowsToZero()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(0);
    }

    [Fact]
    public void BuildIndex_WithSingleJsonObject_ReturnsOneRow()
    {
        // Arrange
        var content = "{\"id\": 1, \"name\": \"Alice\"}";
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }

    [Fact]
    public void BuildIndex_WithLargeFile_ProcessesCorrectly()
    {
        // Arrange: Create file with 10,000 lines (enough to trigger checkpointing)
        var lines = Enumerable
            .Range(0, 10_000)
            .Select(i => $"{{\"id\": {i}, \"name\": \"User{i}\"}}");
        var content = string.Join("\n", lines);
        File.WriteAllText(_testFilePath, content);
        var indexer = new RowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(10_000);
    }
}
