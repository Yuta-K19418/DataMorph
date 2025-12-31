using System.Text;
using DataMorph.Engine.Parsing.Json;
using DataMorph.Engine.Types;
using FluentAssertions;

namespace DataMorph.Tests.Parsing.Json;

public sealed class Utf8JsonScannerTests
{
    [Fact]
    public void ScanJsonArray_WithSimpleObjects_DiscoversColumns()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "name": "Alice"},
            {"id": 2, "name": "Bob"}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.RecordCount.Should().Be(2);
        scanner.DiscoveredColumns.Should().HaveCount(2);
        scanner.DiscoveredColumns.Should().ContainKey("id");
        scanner.DiscoveredColumns.Should().ContainKey("name");
        scanner.DiscoveredColumns["id"].Should().Be(ColumnType.WholeNumber);
        scanner.DiscoveredColumns["name"].Should().Be(ColumnType.Text);
    }

    [Fact]
    public void ScanJsonArray_WithNestedObjects_FlattensWithDotNotation()
    {
        // Arrange
        var json = """
        [
            {"user": {"name": "Alice", "age": 30}},
            {"user": {"name": "Bob", "age": 25}}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.DiscoveredColumns.Should().HaveCount(2);
        scanner.DiscoveredColumns.Should().ContainKey("user.name");
        scanner.DiscoveredColumns.Should().ContainKey("user.age");
        scanner.DiscoveredColumns["user.name"].Should().Be(ColumnType.Text);
        scanner.DiscoveredColumns["user.age"].Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void ScanJsonArray_WithDeeplyNestedObjects_FlattensCorrectly()
    {
        // Arrange
        var json = """
        [
            {"order": {"customer": {"address": {"city": "Tokyo"}}}},
            {"order": {"customer": {"address": {"city": "Osaka"}}}}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.DiscoveredColumns.Should().ContainKey("order.customer.address.city");
        scanner.DiscoveredColumns["order.customer.address.city"].Should().Be(ColumnType.Text);
    }

    [Fact]
    public void ScanJsonArray_WithMixedTypes_CombinesTypesCorrectly()
    {
        // Arrange
        var json = """
        [
            {"value": 42},
            {"value": 3.14}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.DiscoveredColumns["value"].Should().Be(ColumnType.FloatingPoint);
    }

    [Fact]
    public void ScanJsonArray_WithNullValues_HandlesNullCorrectly()
    {
        // Arrange
        var json = """
        [
            {"name": "Alice"},
            {"name": null}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.DiscoveredColumns["name"].Should().Be(ColumnType.Text);
    }

    [Fact]
    public void ScanJsonArray_WithAllNullValues_ReturnsNull()
    {
        // Arrange
        var json = """
        [
            {"value": null},
            {"value": null}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.DiscoveredColumns["value"].Should().Be(ColumnType.Null);
    }

    [Fact]
    public void ScanJsonArray_WithDifferentColumnsPerRecord_DiscoversAllColumns()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "name": "Alice"},
            {"id": 2, "email": "bob@example.com"}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.DiscoveredColumns.Should().HaveCount(3);
        scanner.DiscoveredColumns.Should().ContainKey("id");
        scanner.DiscoveredColumns.Should().ContainKey("name");
        scanner.DiscoveredColumns.Should().ContainKey("email");
    }

    [Fact]
    public void ScanJsonArray_WithMaxRecordsLimit_StopsAtLimit()
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
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes, maxRecordsToScan: 3);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.RecordCount.Should().Be(3);
    }

    [Fact]
    public void ScanJsonArray_WithArrays_SkipsArrays()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "tags": ["a", "b", "c"]},
            {"id": 2, "tags": ["d", "e"]}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.DiscoveredColumns.Should().ContainKey("id");
        scanner.DiscoveredColumns.Should().NotContainKey("tags");
    }

    [Fact]
    public void ScanJsonArray_WithAllDataTypes_InfersTypesCorrectly()
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
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.DiscoveredColumns["id"].Should().Be(ColumnType.WholeNumber);
        scanner.DiscoveredColumns["name"].Should().Be(ColumnType.Text);
        scanner.DiscoveredColumns["price"].Should().Be(ColumnType.FloatingPoint);
        scanner.DiscoveredColumns["active"].Should().Be(ColumnType.Boolean);
        scanner.DiscoveredColumns["created"].Should().Be(ColumnType.Timestamp);
    }

    [Fact]
    public void ScanJsonArray_WithEmptyArray_ReturnsFailure()
    {
        // Arrange
        var json = "[]";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void ScanJsonArray_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        var json = "{not a valid json array}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Expected JSON array");
    }

    [Fact]
    public void ScanJsonArray_WithNonArrayRoot_ReturnsFailure()
    {
        // Arrange
        var json = """{"id": 1, "name": "Alice"}""";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Expected JSON array");
    }

    [Fact]
    public void ScanJsonArray_WithTrailingCommas_HandlesCorrectly()
    {
        // Arrange
        var json = """
        [
            {"id": 1, "name": "Alice",},
            {"id": 2, "name": "Bob",}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.RecordCount.Should().Be(2);
    }

    [Fact]
    public void ScanJsonArray_WithComments_SkipsComments()
    {
        // Arrange
        var json = """
        [
            // This is a comment
            {"id": 1, "name": "Alice"}
        ]
        """;
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var result = scanner.ScanJsonArray(jsonBytes);

        // Assert
        result.IsSuccess.Should().BeTrue();
        scanner.RecordCount.Should().Be(1);
    }

    [Fact]
    public void ScanJsonArray_WithNegativeMaxRecords_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var json = """[{"id": 1}]""";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var scanner = new Utf8JsonScanner();

        // Act
        var act = () => scanner.ScanJsonArray(jsonBytes, maxRecordsToScan: -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
