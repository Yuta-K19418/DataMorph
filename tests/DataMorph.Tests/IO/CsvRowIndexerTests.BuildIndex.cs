using AwesomeAssertions;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.IO;

public sealed partial class CsvRowIndexerTests
{
    [Fact]
    public void BuildIndex_WithSimpleCsv_IndexesAllRows()
    {
        // Arrange
        var content = "header1,header2,header3\nvalue1,value2,value3\nvalue4,value5,value6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // 3 rows total (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithQuotedFieldContainingComma_HandlesCorrectly()
    {
        // Arrange
        var content =
            "name,description,price\n\"Smith, John\",\"A product, with comma\",100\n\"Doe, Jane\",Normal,200";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header + 2 data rows (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithQuotedFieldContainingNewline_HandlesCorrectly()
    {
        // Arrange
        var content =
            "name,description\n\"John\",\"Line1\nLine2\nLine3\"\n\"Jane\",\"Single line\"";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header + 2 data rows (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithEscapedQuotes_HandlesCorrectly()
    {
        // Arrange (RFC 4180: quotes are escaped as "")
        var content =
            "name,quote\n\"John\",\"He said \"\"Hello\"\"\"\n\"Jane\",\"She said \"\"Hi\"\"\"";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header + 2 data rows (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithCRLF_HandlesCorrectly()
    {
        // Arrange
        var content = "header1,header2\r\nvalue1,value2\r\nvalue3,value4";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header + 2 data rows (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithMixedLineEndings_HandlesCorrectly()
    {
        // Arrange
        var content = "header1,header2\nvalue1,value2\r\nvalue3,value4\nvalue5,value6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(4); // Header + 3 data rows (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithEmptyFile_ReturnsZeroRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(0);
    }

    [Fact]
    public void BuildIndex_WithHeaderOnly_ReturnsOneRow()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "header1,header2,header3");
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1); // Header row (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithTrailingNewline_IndexesCorrectly()
    {
        // Arrange
        var content = "header1,header2\nvalue1,value2\nvalue3,value4\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Empty row after trailing newline
    }

    [Fact]
    public void BuildIndex_WithComplexQuotedFields_HandlesCorrectly()
    {
        // Arrange: Mix of quoted and unquoted fields, commas and newlines inside quotes
        var content =
            "name,address,notes\n\"Smith, John\",\"123 Main St\nApt 4\",\"Has a cat\"\n\"Doe, Jane\",\"456 \"\"Oak\"\" Avenue\",\"Likes \"\"pizza\"\"\"\nNormal,Simple,Data";

        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(4); // Header + 3 data rows (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithUnicodeContent_IndexesCorrectly()
    {
        // Arrange
        var content = "名前,説明\n太郎,\"日本語, テスト\"\n花子,シンプル";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header + 2 data rows (no trailing newline)
    }

    [Fact]
    public void BuildIndex_WithOnlyNewlines_IndexesCorrectly()
    {
        // Arrange
        var content = "\n\n\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Three empty rows (newlines terminate rows, not create them)
    }

    [Fact]
    public void BuildIndex_WithQuotedEmptyField_HandlesCorrectly()
    {
        // Arrange
        var content = "col1,col2,col3\nval1,\"\",val3\nval4,\"\",val6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header + 2 data rows (no trailing newline)
    }
}
