using AwesomeAssertions;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.IO;

public sealed partial class CsvDataRowIndexerTests
{
    [Fact]
    public void BuildIndex_WithSimpleCsv_IndexesAllRows()
    {
        // Arrange
        var content = "header1,header2,header3\nvalue1,value2,value3\nvalue4,value5,value6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithQuotedFieldContainingComma_HandlesCorrectly()
    {
        // Arrange
        var content =
            "name,description,price\n\"Smith, John\",\"A product, with comma\",100\n\"Doe, Jane\",Normal,200";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithQuotedFieldContainingNewline_HandlesCorrectly()
    {
        // Arrange
        var content =
            "name,description\n\"John\",\"Line1\nLine2\nLine3\"\n\"Jane\",\"Single line\"";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithEscapedQuotes_HandlesCorrectly()
    {
        // Arrange (RFC 4180: quotes are escaped as "")
        var content =
            "name,quote\n\"John\",\"He said \"\"Hello\"\"\"\n\"Jane\",\"She said \"\"Hi\"\"\"";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithCRLF_HandlesCorrectly()
    {
        // Arrange
        var content = "header1,header2\r\nvalue1,value2\r\nvalue3,value4";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithMixedLineEndings_HandlesCorrectly()
    {
        // Arrange
        var content = "header1,header2\nvalue1,value2\r\nvalue3,value4\nvalue5,value6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithEmptyFile_ReturnsZeroRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, string.Empty);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(0);
    }

    [Fact]
    public void BuildIndex_WithHeaderOnly_ReturnsZeroRows()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "header1,header2,header3");
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(0); // Header is excluded, no data rows
    }

    [Fact]
    public void BuildIndex_WithTrailingNewline_IndexesCorrectly()
    {
        // Arrange
        var content = "header1,header2\nvalue1,value2\nvalue3,value4\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, empty line after trailing newline is ignored (not a data row)
    }

    [Fact]
    public void BuildIndex_WithComplexQuotedFields_HandlesCorrectly()
    {
        // Arrange: Mix of quoted and unquoted fields, commas and newlines inside quotes
        var content =
            "name,address,notes\n\"Smith, John\",\"123 Main St\nApt 4\",\"Has a cat\"\n\"Doe, Jane\",\"456 \"\"Oak\"\" Avenue\",\"Likes \"\"pizza\"\"\"\nNormal,Simple,Data";

        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(3); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithUnicodeContent_IndexesCorrectly()
    {
        // Arrange
        var content = "名前,説明\n太郎,\"日本語, テスト\"\n花子,シンプル";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithOnlyNewlines_IndexesCorrectly()
    {
        // Arrange
        var content = "\n\n\n";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header line is empty line, so 2 data rows (empty lines) remain
    }

    [Fact]
    public void BuildIndex_WithQuotedEmptyField_HandlesCorrectly()
    {
        // Arrange
        var content = "col1,col2,col3\nval1,\"\",val3\nval4,\"\",val6";
        File.WriteAllText(_testFilePath, content);
        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(2); // Header is excluded, only data rows count
    }

    [Fact]
    public void BuildIndex_WithVeryLargeFile_HandlesCorrectly()
    {
        // Arrange: Create file with exactly 1001 rows to test checkpoint boundary
        var lines = new List<string> { "col1,col2" };
        lines.AddRange(Enumerable.Range(1, 1001).Select(i => $"value{i:D4},data{i:D4}"));
        File.WriteAllText(_testFilePath, string.Join("\n", lines));

        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1001);
    }

    [Fact]
    public void BuildIndex_WithPartialQuotesAtBufferBoundary_HandlesCorrectly()
    {
        // Arrange: Create content where quote spans buffer boundary
        var longValue = new string('a', 1024 * 1024); // 1MB value
        var content = $"col1,col2\n\"{longValue}\",normal";
        File.WriteAllText(_testFilePath, content);

        var indexer = new CsvDataRowIndexer(_testFilePath);

        // Act
        indexer.BuildIndex();

        // Assert
        indexer.TotalRows.Should().Be(1);
    }
}
