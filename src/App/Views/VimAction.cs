namespace DataMorph.App.Views;

/// <summary>
/// Represents the navigation action resolved by <see cref="VimKeyTranslator"/>.
/// </summary>
internal enum VimAction
{
    /// <summary>No vim action — key should be forwarded to the base handler.</summary>
    None,

    /// <summary>First 'g' was pressed; waiting for a second 'g' to complete the sequence.</summary>
    PendingGSequence,

    /// <summary>Move the cursor up (k).</summary>
    MoveUp,

    /// <summary>Move the cursor down (j).</summary>
    MoveDown,

    /// <summary>Move the cursor left (h).</summary>
    MoveLeft,

    /// <summary>Move the cursor right (l).</summary>
    MoveRight,

    /// <summary>Jump to the first row/node (gg).</summary>
    GoToFirst,

    /// <summary>Jump to the last row/node (Shift+G).</summary>
    GoToEnd,
}
