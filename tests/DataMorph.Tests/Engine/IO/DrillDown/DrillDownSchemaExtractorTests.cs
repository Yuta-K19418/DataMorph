namespace DataMorph.Tests.Engine.IO.DrillDown;

public sealed class DrillDownSchemaExtractorTests
{
    [Fact]
    public void ExtractFromNode_ArrayOfObjects_ReturnsUnionOfAllTopLevelKeys()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ExtractFromNode_ArrayOfObjectsWithVaryingKeys_ReturnsNullableColumnsForMissingKeys()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ExtractFromNode_ArrayWithNonObjectElement_ReturnsFailure()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ExtractFromNode_EmptyArray_ReturnsFailure()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ExtractFromNode_ArrayOfEmptyObjects_ReturnsFailure()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ExtractFromNode_MalformedJson_ReturnsFailure()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ExtractFromNode_SourceFormatMatchesParameter_ReturnsCorrectFormat()
    {
        // Arrange

        // Act

        // Assert
    }
}
