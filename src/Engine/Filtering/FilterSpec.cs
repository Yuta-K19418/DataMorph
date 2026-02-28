using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;

namespace DataMorph.Engine.Filtering;

/// <summary>
/// Resolved filter specification used internally by <see cref="IFilterRowIndexer"/>
/// implementations.
/// Carries the source column index and effective column type (respecting preceding
/// <c>CastColumnAction</c>s) alongside the operator and comparison value.
/// </summary>
public readonly record struct FilterSpec(
    int SourceColumnIndex,
    ColumnType ColumnType,
    FilterOperator Operator,
    string Value
);
