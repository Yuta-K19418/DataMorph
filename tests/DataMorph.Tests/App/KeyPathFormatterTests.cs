namespace DataMorph.Tests.App;

public sealed class KeyPathFormatterTests
{
    [Fact]
    public void Format_WithEmptyPath_ReturnsRoot()
    {
        // Arrange — an empty path represents the root location

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void Format_WithKeyOnlyPath_ReturnsJoinedKeys()
    {
        // Arrange — object-property segments joined by the breadcrumb separator

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void Format_WithIndexSegment_AndCollapseIndicesTrue_ReturnsStarMarker()
    {
        // Arrange — Full Aggregation DrillDown collapses every index to [*]

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }

    [Fact]
    public void Format_WithIndexSegment_AndCollapseIndicesFalse_ReturnsLiteralIndex()
    {
        // Arrange — tree navigation and Single DrillDown keep the concrete index

        // Act

        // Assert
        Assert.Fail("Not implemented");
    }
}
