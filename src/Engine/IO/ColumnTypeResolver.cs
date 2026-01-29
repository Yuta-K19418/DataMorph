using DataMorph.Engine.Types;

namespace DataMorph.Engine.IO;

/// <summary>
/// Pure type resolution logic for resolving two different column types to their common supertype.
/// </summary>
public static class ColumnTypeResolver
{
    /// <summary>
    /// Resolves two different types to their common supertype.
    /// </summary>
    /// <param name="current">The currently established type.</param>
    /// <param name="observed">The newly observed type.</param>
    /// <returns>The resolved type that can represent both.</returns>
    /// <remarks>
    /// Precondition: current != observed (caller should check before calling).
    /// </remarks>
    public static ColumnType Resolve(ColumnType current, ColumnType observed)
    {
        // Handle the case where current and observed are the same
        if (current == observed)
        {
            return current;
        }

        // Text is the universal fallback - absorbs all other types
        if (current == ColumnType.Text || observed == ColumnType.Text)
        {
            return ColumnType.Text;
        }

        // Handle numeric promotions
        if (current == ColumnType.WholeNumber && observed == ColumnType.FloatingPoint)
        {
            return ColumnType.FloatingPoint;
        }

        if (current == ColumnType.FloatingPoint && observed == ColumnType.WholeNumber)
        {
            return ColumnType.FloatingPoint;
        }

        // Handle incompatible type combinations (resulting in Text)
        // Boolean + anything else (except Boolean) -> Text
        if (current == ColumnType.Boolean || observed == ColumnType.Boolean)
        {
            return ColumnType.Text;
        }

        // Timestamp + anything else (except Timestamp) -> Text
        if (current == ColumnType.Timestamp || observed == ColumnType.Timestamp)
        {
            return ColumnType.Text;
        }

        // WholeNumber + any other incompatible type -> Text
        if (current == ColumnType.WholeNumber || observed == ColumnType.WholeNumber)
        {
            return ColumnType.Text;
        }

        // FloatingPoint + any other incompatible type -> Text
        if (current == ColumnType.FloatingPoint || observed == ColumnType.FloatingPoint)
        {
            return ColumnType.Text;
        }

        // Default fallback - should not reach here as all cases are covered
        return ColumnType.Text;
    }
}
