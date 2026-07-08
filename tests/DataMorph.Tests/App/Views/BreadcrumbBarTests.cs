using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.IO.DrillDown;

namespace DataMorph.Tests.App.Views;

public sealed class BreadcrumbBarTests
{
    [Fact]
    public void SetPath_WithPath_UpdatesDisplayedText()
    {
        // Arrange
        using var bar = new BreadcrumbBar();
        IReadOnlyList<KeyPathSegment> path =
        [
            new KeyPathSegment("data", KeyPathSegmentKind.Key),
            new KeyPathSegment("[0]", KeyPathSegmentKind.Index),
        ];

        // Act
        bar.SetPath(path, collapseIndices: false);

        // Assert
        bar.Text.Should().Be("data[0]");
    }
}
