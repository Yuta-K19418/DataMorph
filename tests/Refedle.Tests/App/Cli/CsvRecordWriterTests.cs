namespace Refedle.Tests.App.Cli;

public sealed class CsvRecordWriterTests
{
    // CSV output is always plain text, so Encoding is ignored; only Presence matters.
    // CellPresence is internal, so each case is standalone.
    [Fact]
    public void WriteCellData_Value_WritesEscapedValue()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_Null_WritesEmpty()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_Missing_WritesEmpty()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_Invalid_WritesEmpty()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }
}
