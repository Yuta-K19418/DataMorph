using System.Text.Json.Serialization;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.Models;

/// <summary>
/// JSON serialization context for DataMorph models.
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
[JsonSerializable(typeof(FilterOperator))]
[JsonSerializable(typeof(ColumnSchema))]
[JsonSerializable(typeof(TableSchema))]
[JsonSerializable(typeof(ColumnType))]
[JsonSerializable(typeof(DataFormat))]
[JsonSerializable(typeof(List<MorphAction>))]
[JsonSerializable(typeof(List<ColumnSchema>))]
public partial class DataMorphJsonContext : JsonSerializerContext
{
}
