using Refedle.App.Cli;

namespace Refedle.Tests.App.Cli;

public sealed class CsvRecordReaderTests
{
    // CSV cells always carry Value presence; the encoding is heuristically classified.
    [Theory]
    [InlineData("007", CellEncoding.Numeric)]
    [InlineData("TRUE", CellEncoding.Boolean)]
    [InlineData("hello", CellEncoding.PlainText)]
    [InlineData("", CellEncoding.PlainText)]
    internal void GetCellData_CsvText_ReturnsValuePresenceAndClassifiedEncoding(string input, CellEncoding expectedEncoding)
    {
        // Arrange

        // Act

        // Assert
        Assert.Fail($"Not implemented: \"{input}\" -> Value/{expectedEncoding}");
    }
}
