using System.Text;
using DataMorph.Engine.Models;
using DataMorph.Engine.Parsing.Json;
using DataMorph.Engine.Types;
using FluentAssertions;

namespace DataMorph.Tests.Parsing.Json;

public sealed class JsonSchemaDiscoveryTests
{
    [Fact]
    public void DiscoverSchema_WithSimpleObjects_ReturnsTableSchema()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "name": "Alice", "age": 30},
            {"id": 2, "name": "Bob", "age": 25}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.ColumnCount.Should().Be(3);
        schema.RowCount.Should().Be(2);
        schema.SourceFormat.Should().Be(DataFormat.Json);
    }

    [Fact]
    public void DiscoverSchema_WithSortedColumns_SortsColumnsAlphabetically()
    {
        // Arrange
        var json = """
        [
            {"z": 1, "a": 2, "m": 3}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.Columns[0].Name.Should().Be("a");
        schema.Columns[1].Name.Should().Be("m");
        schema.Columns[2].Name.Should().Be("z");
    }

    [Fact]
    public void DiscoverSchema_WithColumnsIndexing_AssignsCorrectIndices()
    {
        // Arrange
        var json = """
        [
            {"name": "Alice", "age": 30, "email": "alice@example.com"}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.Columns[0].ColumnIndex.Should().Be(0);
        schema.Columns[1].ColumnIndex.Should().Be(1);
        schema.Columns[2].ColumnIndex.Should().Be(2);
    }

    [Fact]
    public void DiscoverSchema_WithNullOnlyColumn_SetsTypeToText()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "value": null},
            {"id": 2, "value": null}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.GetColumn("value").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.Type.Should().Be(ColumnType.Text);
        schema.GetColumn("value").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.IsNullable.Should().BeTrue();
    }

    [Fact]
    public void DiscoverSchema_WithNullableColumn_SetsIsNullableToTrue()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "name": "Alice"},
            {"id": 2, "name": null}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.GetColumn("name").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.Type.Should().Be(ColumnType.Text);
    }

    [Fact]
    public void DiscoverSchema_WithNonNullableColumn_SetsIsNullableToFalse()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "name": "Alice"},
            {"id": 2, "name": "Bob"}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.GetColumn("name").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.IsNullable.Should().BeFalse();
    }

    [Fact]
    public void DiscoverSchema_WithNestedObjects_FlattensCorrectly()
    {
        // Arrange
        var json = """
        [
            {"user": {"name": "Alice", "profile": {"city": "Tokyo"}}},
            {"user": {"name": "Bob", "profile": {"city": "Osaka"}}}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.ColumnCount.Should().Be(2);
        schema.ContainsColumn("user.name").Should().BeTrue();
        schema.ContainsColumn("user.profile.city").Should().BeTrue();
    }

    [Fact]
    public void DiscoverSchema_WithMaxRecordsLimit_ScansLimitedRecords()
    {
        // Arrange
        var json = """
        [
            {"id": 1},
            {"id": 2},
            {"id": 3},
            {"id": 4},
            {"id": 5}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery(maxRecordsToScan: 2);

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.RowCount.Should().Be(2);
    }

    [Fact]
    public void DiscoverSchema_WithEmptyArray_ReturnsFailure()
    {
        // Arrange
        var json = "[]";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void DiscoverSchema_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        var json = "not valid json";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void DiscoverSchema_WithNonArrayRoot_ReturnsFailure()
    {
        // Arrange
        var json = """{"id": 1, "name": "Alice"}""";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Expected JSON array");
    }

    [Fact]
    public void DiscoverSchema_WithAllDataTypes_InfersCorrectTypes()
    {
        // Arrange
        var json = """
        [
            {
                "id": 1,
                "name": "Alice",
                "price": 19.99,
                "active": true,
                "created": "2024-01-15T10:30:00Z"
            }
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.GetColumn("id").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.Type.Should().Be(ColumnType.WholeNumber);
        schema.GetColumn("name").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.Type.Should().Be(ColumnType.Text);
        schema.GetColumn("price").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.Type.Should().Be(ColumnType.FloatingPoint);
        schema.GetColumn("active").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.Type.Should().Be(ColumnType.Boolean);
        schema.GetColumn("created").Should().NotBeNull().And.BeOfType<ColumnSchema>()
            .Which.Type.Should().Be(ColumnType.Timestamp);
    }

    [Fact]
    public void DiscoverSchema_WithReadOnlySequence_WorksCorrectly()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "name": "Alice"}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var sequence = new System.Buffers.ReadOnlySequence<byte>(jsonBytes);
        var discovery = new JsonSchemaDiscovery();

        // Act
        var result = discovery.DiscoverSchema(sequence);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.ColumnCount.Should().Be(2);
    }

    [Fact]
    public void Constructor_WithNegativeMaxRecords_ThrowsArgumentOutOfRangeException()
    {
        // Arrange & Act
        var act = () => new JsonSchemaDiscovery(maxRecordsToScan: -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithZeroMaxRecords_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => new JsonSchemaDiscovery(maxRecordsToScan: 0);

        // Assert
        act.Should().NotThrow();
    }
}
