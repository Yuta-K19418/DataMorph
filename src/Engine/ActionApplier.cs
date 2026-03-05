using System.Diagnostics;
using DataMorph.Engine.Filtering;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;

namespace DataMorph.Engine;

/// <summary>
/// Translates an action stack and input schema into a format-agnostic
/// <see cref="BatchOutputSchema"/> for batch processing.
/// Pure and stateless: no I/O, no side effects.
/// </summary>
public static class ActionApplier
{
    /// <summary>
    /// Builds a <see cref="BatchOutputSchema"/> by applying the given actions
    /// to the input schema in order.
    /// </summary>
    /// <param name="schema">The inferred input schema.</param>
    /// <param name="actions">The ordered list of actions from the recipe.</param>
    /// <returns>
    /// A <see cref="BatchOutputSchema"/> describing which columns to include
    /// (with their output names) and which filter specs to evaluate.
    /// </returns>
    public static BatchOutputSchema BuildOutputSchema(
        TableSchema schema,
        IReadOnlyList<MorphAction> actions
    )
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(actions);

        // Build working columns copy for tracking state changes
        var workingColumns = schema
            .Columns.Select(c => (c.Name, c.Type, c.ColumnIndex, OutputName: c.Name))
            .ToList();
        var nameToWorkingIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < workingColumns.Count; i++)
        {
            nameToWorkingIndex[workingColumns[i].Name] = i;
        }

        List<FilterSpec> filterSpecs = [];

        // Process actions in order
        foreach (var action in actions)
        {
            if (action is RenameColumnAction rename)
            {
                if (!nameToWorkingIndex.TryGetValue(rename.OldName, out var idx))
                {
                    continue;
                }

                var (name, type, columnIndex, _) = workingColumns[idx];
                workingColumns[idx] = (name, type, columnIndex, rename.NewName);
                nameToWorkingIndex.Remove(rename.OldName);
                nameToWorkingIndex[rename.NewName] = idx;
                continue;
            }

            if (action is DeleteColumnAction delete)
            {
                if (!nameToWorkingIndex.TryGetValue(delete.ColumnName, out _))
                {
                    continue;
                }

                nameToWorkingIndex.Remove(delete.ColumnName);
                continue;
            }

            if (action is CastColumnAction cast)
            {
                if (!nameToWorkingIndex.TryGetValue(cast.ColumnName, out var idx))
                {
                    continue;
                }

                var (name, _, columnIndex, outputName) = workingColumns[idx];
                workingColumns[idx] = (name, cast.TargetType, columnIndex, outputName);
                continue;
            }

            if (action is FilterAction filter)
            {
                if (!nameToWorkingIndex.TryGetValue(filter.ColumnName, out var idx))
                {
                    continue;
                }

                var (_, type, columnIndex, _) = workingColumns[idx];
                filterSpecs.Add(
                    new FilterSpec(
                        SourceColumnIndex: columnIndex,
                        ColumnType: type,
                        Operator: filter.Operator,
                        Value: filter.Value
                    )
                );
                continue;
            }

            throw new UnreachableException($"Unhandled action type: {action.GetType().Name}");
        }

        // Build remaining columns in order (filter out deleted columns)
        var outputColumns = nameToWorkingIndex
            .OrderBy(kvp => kvp.Value)
            .Select(kvp =>
            {
                var (name, _, _, outputName) = workingColumns[kvp.Value];
                return new BatchOutputColumn(SourceName: name, OutputName: outputName);
            })
            .ToList();

        return new BatchOutputSchema(outputColumns, filterSpecs);
    }
}
