using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Abstract base class for all format-specific tree views.
/// Provides Vim-key navigation (h/j/k/l/g/G/Ctrl+d/Ctrl+u), 't'-key table-mode toggle,
/// Enter-to-toggle expand/collapse, and global-shortcut passthrough guard.
/// </summary>
internal abstract class MorphTreeView : TreeView
{
    private readonly VimKeyTranslator _vimKeys = new();
    private readonly Action _onTableModeToggle;

    protected MorphTreeView(Action onTableModeToggle)
    {
        ArgumentNullException.ThrowIfNull(onTableModeToggle);
        _onTableModeToggle = onTableModeToggle;
        Accepted += OnAccepted;
    }

    private void OnAccepted(object? sender, CommandEventArgs e)
    {
        var node = SelectedObject;
        if (node is null)
        {
            return;
        }

        if (IsExpanded(node))
        {
            Collapse(node);
            return;
        }

        Expand(node);
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (key.KeyCode == KeyCode.T)
        {
            _onTableModeToggle();
            return true;
        }

        var action = _vimKeys.Translate(key.KeyCode);

        return action switch
        {
            VimAction.PendingGSequence => true,
            VimAction.MoveDown => ConsumeAction(() =>
                AdjustSelection(offset: 1, expandSelection: false)
            ),
            VimAction.MoveUp => ConsumeAction(() =>
                AdjustSelection(offset: -1, expandSelection: false)
            ),
            VimAction.PageDown => ConsumeAction(() =>
                AdjustSelection(offset: Viewport.Height, expandSelection: false)
            ),
            VimAction.PageUp => ConsumeAction(() =>
                AdjustSelection(offset: -Viewport.Height, expandSelection: false)
            ),
            VimAction.MoveLeft => base.OnKeyDown(new Key(KeyCode.CursorLeft)),
            VimAction.MoveRight => base.OnKeyDown(new Key(KeyCode.CursorRight)),
            VimAction.GoToFirst => ConsumeAction(GoToFirst),
            VimAction.GoToEnd => ConsumeAction(GoToEnd),
            _ => HandleNonVimKey(key),
        };
    }

    private bool HandleNonVimKey(Key key)
    {
        // Prevent global shortcut keys from being consumed by TreeView's incremental search.
        // By returning false, we let these keys bubble up to AppKeyHandler.
        if (AppKeyHandler.IsGlobalShortcut(key.KeyCode))
        {
            return false;
        }

        return base.OnKeyDown(key);
    }

    private static bool ConsumeAction(Action action)
    {
        action();
        return true;
    }
}
