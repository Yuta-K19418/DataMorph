namespace Refedle.Tests.App.Cli;

// End-to-end coverage for the typed CellData channel. Skeleton only: bodies are
// filled in Step 2. These exercise the real readers/writers via Runner.RunAsync.
public sealed partial class RunnerTests
{
    // -------------------------------------------------------------------------
    // JSON Lines → JSON Lines — Object/Array raw JSON preservation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithObjectAndArrayContainingNonAscii_PreservesRawJson()
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    // -------------------------------------------------------------------------
    // JSON Lines → JSON Lines — Number lexical form preservation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("1.50")]
    [InlineData("1e10")]
    [InlineData("9223372036854775808")] // Int64.MaxValue + 1, beyond Int64 range
    public async Task RunAsync_JsonLinesToJsonLines_WithNumberToken_PreservesLexicalForm(string numberLiteral)
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail($"Not implemented: {numberLiteral}");
    }

    // -------------------------------------------------------------------------
    // JSON Lines → JSON Lines — String value handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_WithStringEscapes_ResolvesToPlain()
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Theory]
    [InlineData("5")]
    [InlineData("true")]
    public async Task RunAsync_JsonLinesToJsonLines_WithStringLookingNumericOrBoolean_StaysJsonString(string stringLiteral)
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail($"Not implemented: {stringLiteral}");
    }

    [Theory]
    [InlineData("<null>")]
    [InlineData("<error>")]
    public async Task RunAsync_JsonLinesToJsonLines_WithSentinelLookingString_StaysLiteral(string stringLiteral)
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail($"Not implemented: {stringLiteral}");
    }

    [Fact]
    public async Task RunAsync_JsonLinesToJsonLines_MissingVersusExplicitNull_Distinguishes()
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    // -------------------------------------------------------------------------
    // CSV → JSON Lines — numeric normalization (regression guards)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CsvToJsonLines_WithLeadingZeroNumber_NormalizesToJsonNumber()
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Fact]
    public async Task RunAsync_CsvToJsonLines_WithNumericLookingFill_EmitsJsonNumber()
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }

    [Fact]
    public async Task RunAsync_CsvToJsonLines_WithNumericLookingTimestampFormat_EmitsJsonNumber()
    {
        // Arrange

        // Act

        // Assert
        await Task.CompletedTask;
        Assert.Fail("Not implemented");
    }
}
