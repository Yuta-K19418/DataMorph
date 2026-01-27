using AwesomeAssertions;
using DataMorph.Engine.IO;

namespace DataMorph.Tests.IO;

public sealed class CsvDataRowReaderTests : IDisposable
{
    private readonly string _testFilePath;

    public CsvDataRowReaderTests()
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

    private static string[] ToStringArray(CsvDataRow row)
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
        var reader = new CsvDataRowReader(_testFilePath, columnCount: 3);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - Skip header by using offset, then start reading data rows
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2", "val3"]);
        ToStringArray(rows[1]).Should().Equal(["val4", "val5", "val6"]);
    }

    [Fact]
    public void ReadRows_WithNegativeByteOffset_ReturnsEmptyList()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvDataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - Use offset after header for consistency, but negative offset should still return empty
        var rows = reader.ReadRows(byteOffset: -1, rowsToSkip: 0, rowsToRead: 1);

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ReadRows_WithZeroRowsToRead_ReturnsEmptyList()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvDataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 0);

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public void ReadRows_WithFewerColumnsThanExpected_ThrowsInvalidDataException()
    {
        // Arrange
        var csvContent = "col1,col2,col3\nval1,val2\nval3";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvDataRowReader(_testFilePath, columnCount: 3);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act & Assert
        // Sep.Reader enforces strict column count matching by default
        var act = () => reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);
        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadRows_WithNonZeroByteOffset_ReadsFromCorrectPosition()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvDataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - When reading from offset after header, the first line at that offset is treated as data (HasHeader = false)
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 2);

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2"]);
        ToStringArray(rows[1]).Should().Equal(["val3", "val4"]);
    }

    [Fact]
    public void ReadRows_WithRowsToSkip_SkipsCorrectNumberOfRows()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2\nval3,val4\nval5,val6\nval7,val8";
        File.WriteAllText(_testFilePath, csvContent);
        var reader = new CsvDataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - Skip header by using offset, then skip additional rows
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 1, rowsToRead: 2);

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val3", "val4"]);
        ToStringArray(rows[1]).Should().Equal(["val5", "val6"]);
    }

    [Fact]
    public void ReadRows_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.csv");
        var reader = new CsvDataRowReader(nonExistentPath, columnCount: 2);

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
        var reader = new CsvDataRowReader(_testFilePath, columnCount: 2);
        var offsetAfterHeader = csvContent.IndexOf('\n', StringComparison.Ordinal) + 1;

        // Act - Request 100 rows but only 2 data rows available
        var rows = reader.ReadRows(byteOffset: offsetAfterHeader, rowsToSkip: 0, rowsToRead: 100);

        // Assert
        rows.Count.Should().Be(2);
        ToStringArray(rows[0]).Should().Equal(["val1", "val2"]);
        ToStringArray(rows[1]).Should().Equal(["val3", "val4"]);
    }
}
