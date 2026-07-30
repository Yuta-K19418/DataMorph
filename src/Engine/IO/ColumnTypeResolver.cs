using Refedle.Engine.Types;

namespace Refedle.Engine.IO;

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
        if (current == observed)
        {
            return current;
        }

        // Text/JsonObject/JsonArray are universal fallbacks - any mix with another type absorbs to Text.
        if (IsUniversalFallback(current) || IsUniversalFallback(observed))
        {
            return ColumnType.Text;
        }

        // The only compatible mix among the remaining numeric/scalar types is WholeNumber+FloatingPoint.
        return IsNumericPromotion(current, observed) ? ColumnType.FloatingPoint : ColumnType.Text;
    }

    private static bool IsUniversalFallback(ColumnType type) =>
        type is ColumnType.Text or ColumnType.JsonObject or ColumnType.JsonArray;

    private static bool IsNumericPromotion(ColumnType current, ColumnType observed) =>
        (current == ColumnType.WholeNumber && observed == ColumnType.FloatingPoint)
        || (current == ColumnType.FloatingPoint && observed == ColumnType.WholeNumber);
}
