namespace Refedle.Tests.App.Cli;

public sealed class JsonLinesRecordWriterTests
{
    // CellPresence/CellEncoding are internal, so each Presence x Encoding combination
    // is a standalone case (the enums are constructed inside the test body in Step 2).
    [Fact]
    public void WriteCellData_ValuePlainText_WritesJsonString()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_ValueRaw_WritesRawJson()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_ValueNumeric_WritesJsonNumber()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_ValueBoolean_WritesJsonBoolean()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_Null_WritesJsonNull()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_Missing_OmitsProperty()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void WriteCellData_Invalid_WritesEmptyString()
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }
}
