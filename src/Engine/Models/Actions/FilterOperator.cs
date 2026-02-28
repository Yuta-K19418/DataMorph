using System.Text.Json.Serialization;

namespace DataMorph.Engine.Models.Actions;

/// <summary>
/// Defines the comparison operator for a <see cref="FilterAction"/> condition.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FilterOperator>))]
public enum FilterOperator
{
    /// <summary>Case-insensitive equality: <c>==</c></summary>
    Equals,

    /// <summary>Case-insensitive inequality: <c>!=</c></summary>
    NotEquals,

    /// <summary>Numeric or timestamp greater-than: <c>&gt;</c></summary>
    GreaterThan,

    /// <summary>Numeric or timestamp less-than: <c>&lt;</c></summary>
    LessThan,

    /// <summary>Numeric or timestamp greater-than-or-equal: <c>&gt;=</c></summary>
    GreaterThanOrEqual,

    /// <summary>Numeric or timestamp less-than-or-equal: <c>&lt;=</c></summary>
    LessThanOrEqual,

    /// <summary>Case-insensitive substring match. Applies to <c>Text</c> columns.</summary>
    Contains,

    /// <summary>Negation of <see cref="Contains"/>.</summary>
    NotContains,

    /// <summary>Case-insensitive prefix match. Applies to <c>Text</c> columns.</summary>
    StartsWith,

    /// <summary>Case-insensitive suffix match. Applies to <c>Text</c> columns.</summary>
    EndsWith,
}
