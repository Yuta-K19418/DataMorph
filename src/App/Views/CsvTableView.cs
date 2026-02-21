using DataMorph.App.Views.Dialogs;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;
using Terminal.Gui.Drivers;
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

    /// <summary>
    /// Callback invoked when the user confirms a column morphing action.
    /// <see langword="null"/> means morphing is disabled for this view instance.
    /// </summary>
    internal Action<MorphAction>? OnMorphAction { get; init; }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (key.KeyCode == (KeyCode.R | KeyCode.ShiftMask))
        {
            return HandleRenameColumn();
        }

        if (key.KeyCode == (KeyCode.D | KeyCode.ShiftMask))
        {
            return HandleDeleteColumn();
        }

        if (key.KeyCode == (KeyCode.C | KeyCode.ShiftMask))
        {
            return HandleCastColumn();
        }

        if (key.KeyCode == (KeyCode.F | KeyCode.ShiftMask))
        {
            return HandleFilterColumn();
        }

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

    private bool HandleRenameColumn()
    {
        if (App is null || OnMorphAction is null || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var columnName = Table.ColumnNames[SelectedColumn];
        using var dialog = new RenameColumnDialog(columnName);
        App.Run(dialog);

        if (!dialog.Confirmed || dialog.NewName is null)
        {
            return true;
        }

        OnMorphAction(new RenameColumnAction { OldName = columnName, NewName = dialog.NewName });
        return true;
    }

    private bool HandleDeleteColumn()
    {
        if (App is null || OnMorphAction is null || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var columnName = Table.ColumnNames[SelectedColumn];
        using var dialog = new DeleteColumnDialog(columnName);
        App.Run(dialog);

        if (!dialog.Confirmed)
        {
            return true;
        }

        OnMorphAction(new DeleteColumnAction { ColumnName = columnName });
        return true;
    }

    private bool HandleCastColumn()
    {
        if (App is null || OnMorphAction is null || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var columnName = Table.ColumnNames[SelectedColumn];
        using var dialog = new CastColumnDialog(columnName, ColumnType.Text);
        App.Run(dialog);

        if (!dialog.Confirmed || dialog.SelectedType is null)
        {
            return true;
        }

        OnMorphAction(
            new CastColumnAction { ColumnName = columnName, TargetType = dialog.SelectedType.Value }
        );
        return true;
    }

    private bool HandleFilterColumn()
    {
        throw new NotImplementedException();
    }
}
