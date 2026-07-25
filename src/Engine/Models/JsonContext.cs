using System.Text.Json.Serialization;
using Refedle.Engine.Models.Actions;
using Refedle.Engine.Types;

namespace Refedle.Engine.Models;

/// <summary>
/// JSON serialization context for Refedle models.
/// Uses System.Text.Json Source Generators for Native AOT compatibility.
/// Provides high-performance, zero-reflection JSON serialization.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(MorphAction))]
[JsonSerializable(typeof(RenameColumnAction))]
[JsonSerializable(typeof(DeleteColumnAction))]
[JsonSerializable(typeof(CastColumnAction))]
[JsonSerializable(typeof(FilterAction))]
[JsonSerializable(typeof(FillColumnAction))]
[JsonSerializable(typeof(FormatTimestampAction))]
[JsonSerializable(typeof(FilterOperator))]
[JsonSerializable(typeof(ColumnSchema))]
[JsonSerializable(typeof(TableSchema))]
[JsonSerializable(typeof(ColumnType))]
[JsonSerializable(typeof(DataFormat))]
[JsonSerializable(typeof(List<MorphAction>))]
[JsonSerializable(typeof(List<ColumnSchema>))]
public partial class JsonContext : JsonSerializerContext
{
}
