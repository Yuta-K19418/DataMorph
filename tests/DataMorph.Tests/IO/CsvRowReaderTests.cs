using AwesomeAssertions;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.IO;

public sealed class CsvRowReaderTests : IDisposable
{
    private readonly string _testFilePath;

    public CsvRowReaderTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"csvRowReader_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    private static string[] ToStringArray(CsvRow row)
    {
        var result = new string[row.Count];
        for (var i = 0; i < row.Count; i++)
        {
            result[i] = row[i].IsEmpty ? string.Empty : new string(row[i].Span);
        }

        return result;
    }

    [Fact]
    public void ReadRows_WithValidData_ReturnsCorrectRows()
    {
        // Arrange
        var csvContent = "col1,col2,col3\nval1,val2,val3\nval4,val5,val6\nval7,val8,val9";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvRowReader(_testFilePath, columnCount: 3);

        // Act - Sep.Reader automatically reads header, so skip=0 starts at first data row
        var rows = reader.ReadRows(byteOffset: 0, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2", "val3"]);
        ToStringArray(rows[1]).Should().Equal(["val4", "val5", "val6"]);
    }

    [Fact]
    public void ReadRows_WithNegativeByteOffset_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2");
        var reader = new CsvRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.ReadRows(byteOffset: -1, rowsToSkip: 0, rowsToRead: 1);

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ReadRows_WithZeroRowsToRead_ReturnsEmptyList()
    {
        // Arrange
        File.WriteAllText(_testFilePath, "col1,col2\nval1,val2");
        var reader = new CsvRowReader(_testFilePath, columnCount: 2);

        // Act
        var rows = reader.ReadRows(byteOffset: 0, rowsToSkip: 0, rowsToRead: 0);

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ReadRows_WithFewerColumnsThanExpected_ThrowsInvalidDataException()
    {
        // Arrange
        var csvContent = "col1,col2,col3\nval1,val2\nval3";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvRowReader(_testFilePath, columnCount: 3);

        // Act & Assert
        // Sep.Reader enforces strict column count matching by default
        var act = () => reader.ReadRows(byteOffset: 0, rowsToSkip: 1, rowsToRead: 2);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadRows_WithNonZeroByteOffset_ReadsFromCorrectPosition()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - When reading from a non-zero offset, Sep.Reader treats the first line at that offset as header
        // So val1,val2 becomes header, and val3,val4 becomes first data row
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val3", "val4"]);
        ToStringArray(rows[1]).Should().Equal(["val5", "val6"]);
    }

    [Fact]
    public void ReadRows_WithRowsToSkip_SkipsCorrectNumberOfRows()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6\nval7,val8";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvRowReader(_testFilePath, columnCount: 2);

        // Act - Header is auto-read, skip 1 data row, then read 2 rows
        var rows = reader.ReadRows(byteOffset: 0, rowsToSkip: 1, rowsToRead: 2);

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val3", "val4"]);
        ToStringArray(rows[1]).Should().Equal(["val5", "val6"]);
    }

    [Fact]
    public void ReadRows_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(
            Path.GetTempPath(),
            $"nonexistent_{Guid.NewGuid()}.csv"
        );
        var reader = new CsvRowReader(nonExistentPath, columnCount: 2);

        // Act
        var rows = reader.ReadRows(byteOffset: 0, rowsToSkip: 0, rowsToRead: 10);

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ReadRows_WithMoreRowsRequestedThanAvailable_ReturnsAvailableRows()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvRowReader(_testFilePath, columnCount: 2);

        // Act - Request 100 rows but only 2 data rows available
        var rows = reader.ReadRows(byteOffset: 0, rowsToSkip: 0, rowsToRead: 100);

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2"]);
        ToStringArray(rows[1]).Should().Equal(["val3", "val4"]);
    }
}
