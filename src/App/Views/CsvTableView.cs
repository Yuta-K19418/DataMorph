using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// A TableView for CSV data that adds vim-like key navigation
/// (h/j/k/l, gg, Shift+G) as alternatives to arrow keys.
/// </summary>
internal sealed class CsvTableView : TableView
{
    private readonly VimKeyTranslator _vimKeys = new();

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        var action = _vimKeys.Translate(key.KeyCode);

        return action switch
        {
            VimAction.PendingGSequence => true,
            VimAction.MoveDown => ConsumeAction(() =>
                ChangeSelectionByOffset(offsetX: 0, offsetY: 1, extendExistingSelection: false)
            ),
            VimAction.MoveUp => ConsumeAction(() =>
                ChangeSelectionByOffset(offsetX: 0, offsetY: -1, extendExistingSelection: false)
            ),
            VimAction.MoveLeft => ConsumeAction(() =>
                ChangeSelectionByOffset(offsetX: -1, offsetY: 0, extendExistingSelection: false)
            ),
            VimAction.MoveRight => ConsumeAction(() =>
                ChangeSelectionByOffset(offsetX: 1, offsetY: 0, extendExistingSelection: false)
            ),
            VimAction.GoToFirst => ConsumeAction(() =>
                ChangeSelectionByOffset(
                    offsetX: 0,
                    offsetY: -SelectedRow,
                    extendExistingSelection: false
                )
            ),
            VimAction.GoToEnd => ConsumeAction(() =>
                ChangeSelectionByOffset(
                    offsetX: 0,
                    offsetY: Table.Rows - 1 - SelectedRow,
                    extendExistingSelection: false
                )
            ),
            _ => base.OnKeyDown(key),
        };
    }

    private static bool ConsumeAction(Action action)
    {
        action();
        return true;
    }
}
