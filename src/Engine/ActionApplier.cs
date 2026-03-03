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
        throw new NotImplementedException();
    }
}
