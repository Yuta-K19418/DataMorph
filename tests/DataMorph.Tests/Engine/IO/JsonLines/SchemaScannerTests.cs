using System.Text.Json;
using AwesomeAssertions;
using DataMorph.Engine.Models;
using DataMorph.Engine.Types;

namespace DataMorph.Tests.Engine.IO.JsonLines;

public sealed partial class SchemaScannerTests
{
    private static ReadOnlyMemory<byte> Line(string json) =>
        new ReadOnlyMemory<byte>(
            JsonSerializer.SerializeToUtf8Bytes(JsonDocument.Parse(json).RootElement)
        );

    private static void AssertColumn(
        TableSchema schema,
        int index,
        string expectedName,
        ColumnType expectedType,
        bool expectedNullable
    )
    {
        schema.Columns.Should().HaveCountGreaterThan(index);
        var column = schema.Columns[index];
        column.Name.Should().Be(expectedName);
        column.Type.Should().Be(expectedType);
        column.IsNullable.Should().Be(expectedNullable);
    }
}
