using System.Text.Json;
using AwesomeAssertions;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Models;

public sealed class SchemaSerializationTests
{
    [Fact]
    public void ColumnSchema_SerializesAndDeserializes_Correctly()
    {
        // Arrange
        var schema = new ColumnSchema
        {
            Name = "user.email",
            Type = ColumnType.Text,
            IsNullable = false,
            ColumnIndex = 3,
            DisplayFormat = null
        };

        // Act
        var json = JsonSerializer.Serialize(schema, DataMorphJsonContext.Default.ColumnSchema);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.ColumnSchema);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Should().BeEquivalentTo(schema);
    }

    [Fact]
    public void ColumnSchema_WithAllProperties_SerializesCorrectly()
    {
        // Arrange
        var schema = new ColumnSchema
        {
            Name = "created_at",
            Type = ColumnType.Timestamp,
            IsNullable = true,
            ColumnIndex = 0,
            DisplayFormat = "yyyy-MM-dd HH:mm:ss"
        };

        // Act
        var json = JsonSerializer.Serialize(schema, DataMorphJsonContext.Default.ColumnSchema);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.ColumnSchema);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.DisplayFormat.Should().Be("yyyy-MM-dd HH:mm:ss");
    }

    [Fact]
    public void TableSchema_SerializesAndDeserializes_Correctly()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 1 },
                new() { Name = "price", Type = ColumnType.FloatingPoint, ColumnIndex = 2 }
            },
            RowCount = 1000,
            SourceFormat = DataFormat.JsonArray
        };

        // Act
        var json = JsonSerializer.Serialize(schema, DataMorphJsonContext.Default.TableSchema);
        var deserialized = JsonSerializer.Deserialize(json, DataMorphJsonContext.Default.TableSchema);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.ColumnCount.Should().Be(3);
        deserialized.RowCount.Should().Be(1000);
        deserialized.SourceFormat.Should().Be(DataFormat.JsonArray);
        deserialized.Columns.Should().HaveCount(3);
    }

    [Fact]
    public void TableSchema_GetColumn_ReturnsCorrectColumn()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 1 }
            },
            RowCount = 0,
            SourceFormat = DataFormat.JsonArray
        };

        // Act
        var column = schema.GetColumn("name");

        // Assert
        column.Should().NotBeNull();
        column.Name.Should().Be("name");
        column.Type.Should().Be(ColumnType.Text);
    }

    [Fact]
    public void TableSchema_GetColumn_WithNonExistentName_ReturnsNull()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 }
            },
            RowCount = 0,
            SourceFormat = DataFormat.JsonArray
        };

        // Act
        var column = schema.GetColumn("nonexistent");

        // Assert
        column.Should().BeNull();
    }

    [Fact]
    public void TableSchema_ContainsColumn_ReturnsTrueForExistingColumn()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 1 }
            },
            RowCount = 0,
            SourceFormat = DataFormat.JsonArray
        };

        // Act & Assert
        schema.ContainsColumn("id").Should().BeTrue();
        schema.ContainsColumn("name").Should().BeTrue();
        schema.ContainsColumn("age").Should().BeFalse();
    }

    [Fact]
    public void TableSchema_ColumnCount_ReturnsCorrectValue()
    {
        // Arrange
        var schema = new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "col1", Type = ColumnType.Text, ColumnIndex = 0 },
                new() { Name = "col2", Type = ColumnType.WholeNumber, ColumnIndex = 1 },
                new() { Name = "col3", Type = ColumnType.Boolean, ColumnIndex = 2 }
            },
            RowCount = 0,
            SourceFormat = DataFormat.JsonArray
        };

        // Act & Assert
        schema.ColumnCount.Should().Be(3);
    }

    [Fact]
    public void ColumnSchema_SerializedJson_UsesCamelCaseNaming()
    {
        // Arrange
        var schema = new ColumnSchema
        {
            Name = "test",
            Type = ColumnType.Text,
            IsNullable = true,
            ColumnIndex = 0,
            DisplayFormat = "test-format"
        };

        // Act
        var json = JsonSerializer.Serialize(schema, DataMorphJsonContext.Default.ColumnSchema);

        // Assert
        json.Should().Contain("\"name\":");
        json.Should().Contain("\"type\":");
        json.Should().Contain("\"isNullable\":");
        json.Should().Contain("\"columnIndex\":");
        json.Should().Contain("\"displayFormat\":");
        json.Should().NotContain("\"Name\":");
        json.Should().NotContain("\"Type\":");
        json.Should().NotContain("\"IsNullable\":");
        json.Should().NotContain("\"ColumnIndex\":");
        json.Should().NotContain("\"DisplayFormat\":");
    }

    [Fact]
    public void TableSchema_WithDuplicateColumnNames_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 1 },
                new() { Name = "id", Type = ColumnType.Text, ColumnIndex = 2 }
            },
            RowCount = 0,
            SourceFormat = DataFormat.JsonArray
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Duplicate column name found: id*")
            .WithParameterName("Columns");
    }

    [Fact]
    public void TableSchema_WithMultipleDuplicateColumnNames_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 1 },
                new() { Name = "id", Type = ColumnType.Text, ColumnIndex = 2 },
                new() { Name = "name", Type = ColumnType.Text, ColumnIndex = 3 }
            },
            RowCount = 0,
            SourceFormat = DataFormat.JsonArray
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Duplicate column name found: *")
            .WithParameterName("Columns");
    }

    [Fact]
    public void ColumnSchema_WithNullName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new ColumnSchema
        {
            Name = null!,
            Type = ColumnType.Text,
            ColumnIndex = 0
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ColumnSchema_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new ColumnSchema
        {
            Name = string.Empty,
            Type = ColumnType.Text,
            ColumnIndex = 0
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ColumnSchema_WithWhiteSpaceName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new ColumnSchema
        {
            Name = "   ",
            Type = ColumnType.Text,
            ColumnIndex = 0
        };

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ColumnSchema_WithNegativeColumnIndex_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new ColumnSchema
        {
            Name = "test",
            Type = ColumnType.Text,
            ColumnIndex = -1
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TableSchema_WithNegativeRowCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 }
            },
            RowCount = -1,
            SourceFormat = DataFormat.JsonArray
        };

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TableSchema_WithZeroRowCount_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => new TableSchema
        {
            Columns = new List<ColumnSchema>
            {
                new() { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 }
            },
            RowCount = 0,
            SourceFormat = DataFormat.JsonArray
        };

        // Assert
        act.Should().NotThrow();
    }
}
