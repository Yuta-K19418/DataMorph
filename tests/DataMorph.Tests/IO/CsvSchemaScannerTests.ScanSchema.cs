using System.Globalization;
using AwesomeAssertions;
using DataMorph.Engine.IO;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.IO;

public sealed partial class CsvSchemaScannerTests
{
    [Fact]
    public void ScanSchema_SimpleCsvWithMixedTypes_ReturnsCorrectSchema()
    {
        // Arrange
        var columnNames = new[] { "id", "name", "age", "salary", "active", "created_at" };
        var row = new[]
        {
            "1".AsMemory(),
            "Alice".AsMemory(),
            "30".AsMemory(),
            "50000.50".AsMemory(),
            "true".AsMemory(),
            "2024-01-15".AsMemory(),
        };
        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;
        schema.ColumnCount.Should().Be(6);
        schema.SourceFormat.Should().Be(DataFormat.Csv);

        // Check each column
        AssertColumn(schema, 0, "id", ColumnType.WholeNumber, false);
        AssertColumn(schema, 1, "name", ColumnType.Text, false);
        AssertColumn(schema, 2, "age", ColumnType.WholeNumber, false);
        AssertColumn(schema, 3, "salary", ColumnType.FloatingPoint, false);
        AssertColumn(schema, 4, "active", ColumnType.Boolean, false);
        AssertColumn(schema, 5, "created_at", ColumnType.Timestamp, false);
    }

    [Fact]
    public void ScanSchema_WithNullableColumns_MarksNullableCorrectly()
    {
        // Arrange
        var columnNames = new[] { "id", "name", "optional_field" };
        var row = new[]
        {
            "1".AsMemory(),
            "Alice".AsMemory(),
            "".AsMemory(), // Empty value
        };

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        schema.Columns[0].Name.Should().Be("id");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("name");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("optional_field");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeTrue(); // Nullable because empty
    }

    [Fact]
    public void ScanSchema_WithWhitespaceOnlyValue_MarksNullable()
    {
        // Arrange
        var columnNames = new[] { "id", "space_field", "tab_field" };
        var row = new[]
        {
            "1".AsMemory(),
            "   ".AsMemory(), // Whitespace-only
            "\t\n\r ".AsMemory(), // Various whitespace characters
        };

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        schema.Columns[0].Name.Should().Be("id");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("space_field");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeTrue(); // Nullable

        schema.Columns[2].Name.Should().Be("tab_field");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeTrue(); // Nullable
    }

    [Fact]
    public void ScanSchema_EmptyColumnNames_GeneratesColumnNames()
    {
        // Arrange
        var columnNames = new[] { "", "  ", "name" }; // First two are empty/whitespace
        var row = new[] { "1".AsMemory(), "test".AsMemory(), "Alice".AsMemory() };

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        // Empty column names should be replaced with generated names
        schema.Columns[0].Name.Should().Be("Column1");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("Column2");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("name");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_NoColumns_ReturnsFailure()
    {
        // Arrange
        var columnNames = Array.Empty<string>();
        var row = Array.Empty<ReadOnlyMemory<char>>();

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("CSV has no columns");
    }

    [Fact]
    public void ScanSchema_MismatchedColumnCount_ReturnsFailure()
    {
        // Arrange
        var columnNames = new[] { "id", "name", "age" };
        var row = new[]
        {
            "1".AsMemory(),
            "Alice".AsMemory(),
            // Missing third column
        };

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Row has 2 columns but header has 3 columns");
    }

    [Fact]
    public void ScanSchema_HeaderOnlyNoData_AllColumnsTextAndNullable()
    {
        // Arrange
        var columnNames = new[] { "id", "name", "age" };
        var row = new[]
        {
            "".AsMemory(), // Empty values
            "".AsMemory(),
            "".AsMemory(),
        }; // No data rows

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        // All columns should be Text and nullable when values are empty
        schema.Columns[0].Name.Should().Be("id");
        schema.Columns[0].Type.Should().Be(ColumnType.Text);
        schema.Columns[0].IsNullable.Should().BeTrue();

        schema.Columns[1].Name.Should().Be("name");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeTrue();

        schema.Columns[2].Name.Should().Be("age");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeTrue();
    }

    [Fact]
    public void ScanSchema_ComplexNumericValues_DetectsCorrectTypes()
    {
        // Arrange
        var columnNames = new[] { "int", "negative_int", "decimal", "scientific", "zero" };
        var row = new[]
        {
            "123456789".AsMemory(),
            "-987654321".AsMemory(),
            "3.14159265359".AsMemory(),
            "1.234E+10".AsMemory(),
            "0".AsMemory(),
        };

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        schema.Columns[0].Name.Should().Be("int");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("negative_int");
        schema.Columns[1].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("decimal");
        schema.Columns[2].Type.Should().Be(ColumnType.FloatingPoint);
        schema.Columns[2].IsNullable.Should().BeFalse();

        schema.Columns[3].Name.Should().Be("scientific");
        schema.Columns[3].Type.Should().Be(ColumnType.FloatingPoint);
        schema.Columns[3].IsNullable.Should().BeFalse();

        schema.Columns[4].Name.Should().Be("zero");
        schema.Columns[4].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[4].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_DateFormats_DetectsTimestamp()
    {
        // Arrange
        var columnNames = new[] { "iso_date", "iso_datetime", "common_date", "datetime_with_tz" };
        var row = new[]
        {
            "2024-12-31".AsMemory(),
            "2024-12-31T23:59:59".AsMemory(),
            "12/31/2024".AsMemory(),
            "2024-12-31T23:59:59Z".AsMemory(),
        };

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        schema.Columns[0].Name.Should().Be("iso_date");
        schema.Columns[0].Type.Should().Be(ColumnType.Timestamp);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("iso_datetime");
        schema.Columns[1].Type.Should().Be(ColumnType.Timestamp);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("common_date");
        schema.Columns[2].Type.Should().Be(ColumnType.Timestamp);
        schema.Columns[2].IsNullable.Should().BeFalse();

        schema.Columns[3].Name.Should().Be("datetime_with_tz");
        schema.Columns[3].Type.Should().Be(ColumnType.Timestamp);
        schema.Columns[3].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_BooleanVariations_DetectsBoolean()
    {
        // Arrange
        var columnNames = new[] { "lower", "upper", "mixed", "with_spaces" };
        var row = new[]
        {
            "true".AsMemory(),
            "FALSE".AsMemory(),
            "True".AsMemory(),
            "  false  ".AsMemory(),
        };

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        schema.Columns[0].Name.Should().Be("lower");
        schema.Columns[0].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("upper");
        schema.Columns[1].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("mixed");
        schema.Columns[2].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[2].IsNullable.Should().BeFalse();

        schema.Columns[3].Name.Should().Be("with_spaces");
        schema.Columns[3].Type.Should().Be(ColumnType.Boolean);
        schema.Columns[3].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_TextFallback_ForNonParsableValues()
    {
        // Arrange
        var columnNames = new[] { "text", "mixed_alphanum", "invalid_date", "special_chars" };
        var row = new[]
        {
            "hello world".AsMemory(),
            "abc123def".AsMemory(),
            "2024-13-45".AsMemory(), // Invalid date
            "@#$%^&*()".AsMemory(),
        };

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        schema.Columns[0].Name.Should().Be("text");
        schema.Columns[0].Type.Should().Be(ColumnType.Text);
        schema.Columns[0].IsNullable.Should().BeFalse();

        schema.Columns[1].Name.Should().Be("mixed_alphanum");
        schema.Columns[1].Type.Should().Be(ColumnType.Text);
        schema.Columns[1].IsNullable.Should().BeFalse();

        schema.Columns[2].Name.Should().Be("invalid_date");
        schema.Columns[2].Type.Should().Be(ColumnType.Text);
        schema.Columns[2].IsNullable.Should().BeFalse();

        schema.Columns[3].Name.Should().Be("special_chars");
        schema.Columns[3].Type.Should().Be(ColumnType.Text);
        schema.Columns[3].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_TypePriority_BooleanOverNumber()
    {
        // "1" could be interpreted as WholeNumber, but not as Boolean
        // Test ensures Boolean detection only for "true"/"false"
        var columnNames = new[] { "numeric_string" };
        var row = new[] { "1".AsMemory() };

        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        // "1" should be WholeNumber, not Boolean
        schema.Columns[0].Name.Should().Be("numeric_string");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_TypePriority_WholeNumberOverFloatingPoint()
    {
        // "123" should be WholeNumber, not FloatingPoint
        var columnNames = new[] { "integer" };
        var row = new[] { "123".AsMemory() };

        var result = CsvSchemaScanner.ScanSchema(columnNames, [row]);

        result.IsSuccess.Should().BeTrue();
        var schema = result.Value;

        schema.Columns[0].Name.Should().Be("integer");
        schema.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        schema.Columns[0].IsNullable.Should().BeFalse();
    }

    [Fact]
    public void ScanSchema_WithNegativeInitialScanCount_ThrowsException()
    {
        // Arrange
        var columnNames = new[] { "id" };
        var row = new[] { "1".AsMemory() };

        // Act & Assert
        Action action = () => CsvSchemaScanner.ScanSchema(columnNames, [row], initialScanCount: -1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ScanSchema_NullParameters_ThrowsException()
    {
        // Arrange
        var columnNames = new[] { "id" };
        var row = new[] { "1".AsMemory() };

        // Act & Assert
        Action action1 = () => CsvSchemaScanner.ScanSchema(null!, [row]);
        action1.Should().Throw<ArgumentNullException>();

        Action action2 = () => CsvSchemaScanner.ScanSchema(columnNames, null!);
        action2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScanSchema_WithInitialScanCountLimitedTo200_ReturnsCorrectSchema()
    {
        // Arrange
        var columnNames = new[] { "id", "value" };
        var rows = Enumerable
            .Range(0, 300)
            .Select(i =>
                new[]
                {
                    i.ToString(CultureInfo.InvariantCulture).AsMemory(),
                    // Even rows: floating point values, odd rows: whole numbers
                    (
                        i % 2 == 0
                            ? (i * 10 + 0.5).ToString(CultureInfo.InvariantCulture)
                            : (i * 10).ToString(CultureInfo.InvariantCulture)
                    ).AsMemory(),
                }
            )
            .ToList();

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, rows, initialScanCount: 200);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ColumnCount.Should().Be(2);
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
        // Second column should be FloatingPoint because it contains decimal values
        result.Value.Columns[1].Type.Should().Be(ColumnType.FloatingPoint);
    }

    [Fact]
    public void ScanSchema_WithLessThan200Rows_ScansAllRows()
    {
        // Arrange
        var columnNames = new[] { "id" };
        var rows = Enumerable
            .Range(0, 100)
            .Select(i => new[] { i.ToString(CultureInfo.InvariantCulture).AsMemory() })
            .ToList();

        // Act
        var result = CsvSchemaScanner.ScanSchema(columnNames, rows, initialScanCount: 200);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ColumnCount.Should().Be(1);
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
    }

    [Fact]
    public void ScanSchema_WithCustomInitialScanCount_RespectsLimit()
    {
        // Arrange
        var columnNames = new[] { "id" };
        var rows = Enumerable
            .Range(0, 150)
            .Select(i => new[] { i.ToString(CultureInfo.InvariantCulture).AsMemory() })
            .ToList();

        // Act - limit with custom initial scan count of 50
        var result = CsvSchemaScanner.ScanSchema(columnNames, rows, initialScanCount: 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ColumnCount.Should().Be(1);
        result.Value.Columns[0].Type.Should().Be(ColumnType.WholeNumber);
    }
}
