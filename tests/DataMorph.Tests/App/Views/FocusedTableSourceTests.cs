namespace DataMorph.Tests.App.Views;

public sealed class FocusedTableSourceTests
{
    [Fact]
    public void Rows_ReturnsChildCount()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Columns_ReturnsSchemaColumnCountPlusOne()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void ColumnNames_FirstColumnIsHash()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Indexer_JsonLines_HashColumnFormatsAsLineNumberColonIndex()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Indexer_JsonArray_HashColumnFormatsAsElementIndexColonIndex()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Indexer_JsonObject_HashColumnFormatsAsBracketIndex()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Indexer_NonHashColumn_DelegatesToJsonObjectCellExtractor()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Indexer_NegativeRow_ThrowsArgumentOutOfRangeException()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Indexer_RowBeyondBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Indexer_NegativeCol_ThrowsArgumentOutOfRangeException()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public void Indexer_ColBeyondBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange

        // Act

        // Assert
    }
}
