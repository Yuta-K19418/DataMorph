using System.Text.Json.Serialization;

namespace DataMorph.Engine.Models.Actions;

/// <summary>
/// Base class for all data transformation actions in the Action Stack.
/// Designed for AOT-compatible JSON serialization using System.Text.Json Source Generators.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RenameColumnAction), typeDiscriminator: "rename")]
[JsonDerivedType(typeof(DeleteColumnAction), typeDiscriminator: "delete")]
[JsonDerivedType(typeof(CastColumnAction), typeDiscriminator: "cast")]
[JsonDerivedType(typeof(FilterAction), typeDiscriminator: "filter")]
public abstract record MorphAction
{
    /// <summary>
    /// Human-readable description of the action for debugging and UI display.
    /// </summary>
    public abstract string Description { get; }
}
