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

    /// <summary>
    /// Optional predicate that returns <see langword="true"/> when the row indexer's
    /// <c>BuildIndex</c> has completed. When <see langword="null"/>, the guard is skipped.
    /// The <c>Shift+F</c> filter action is blocked until this returns <see langword="true"/>.
    /// </summary>
    internal Func<bool>? IsRowIndexComplete { get; init; }

    /// <summary>
    /// Resolves a column index to the raw (un-labeled) column name for action creation.
    /// When <see langword="null"/>, morphing is disabled (same guard as <see cref="OnMorphAction"/>).
    /// </summary>
    internal Func<int, string>? GetRawColumnName { get; init; }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (Table is null)
        {
            throw new InvalidOperationException("Table cannot be null");
        }

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

        if (key.KeyCode == (KeyCode.L | KeyCode.ShiftMask))
        {
            return HandleFillColumn();
        }

        if (key.KeyCode == (KeyCode.T | KeyCode.ShiftMask))
        {
            return HandleFormatTimestamp();
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
        if (App is null || OnMorphAction is null || GetRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var rawName = GetRawColumnName(SelectedColumn);
        using var dialog = new RenameColumnDialog(rawName);
        App.Run(dialog);

        if (!dialog.Confirmed || dialog.NewName is null)
        {
            return true;
        }

        OnMorphAction(new RenameColumnAction { OldName = rawName, NewName = dialog.NewName });
        return true;
    }

    private bool HandleDeleteColumn()
    {
        if (App is null || OnMorphAction is null || GetRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = GetRawColumnName(SelectedColumn);
        using var dialog = new DeleteColumnDialog(displayName);
        App.Run(dialog);

        if (!dialog.Confirmed)
        {
            return true;
        }

        OnMorphAction(new DeleteColumnAction { ColumnName = rawName });
        return true;
    }

    private bool HandleCastColumn()
    {
        if (App is null || OnMorphAction is null || GetRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = GetRawColumnName(SelectedColumn);
        using var dialog = new CastColumnDialog(displayName, ColumnType.Text);
        App.Run(dialog);

        if (!dialog.Confirmed || dialog.SelectedType is null)
        {
            return true;
        }

        OnMorphAction(
            new CastColumnAction { ColumnName = rawName, TargetType = dialog.SelectedType.Value }
        );
        return true;
    }

    private bool HandleFilterColumn()
    {
        if (App is null || OnMorphAction is null || GetRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        if (IsRowIndexComplete is not null && !IsRowIndexComplete())
        {
            MessageBox.ErrorQuery(App, "Filter", "Row index is still being built. Please wait.", "OK");
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = GetRawColumnName(SelectedColumn);
        using var dialog = new FilterColumnDialog(displayName);
        App.Run(dialog);

        if (!dialog.Confirmed || dialog.SelectedOperator is null || dialog.Value is null)
        {
            return true;
        }

        OnMorphAction(
            new FilterAction
            {
                ColumnName = rawName,
                Operator = dialog.SelectedOperator.Value,
                Value = dialog.Value,
            }
        );
        return true;
    }

    private bool HandleFillColumn()
    {
        if (App is null || OnMorphAction is null || GetRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = GetRawColumnName(SelectedColumn);
        using var dialog = new FillColumnDialog(displayName);
        App.Run(dialog);

        if (!dialog.Confirmed)
        {
            return true;
        }

        OnMorphAction(new FillColumnAction { ColumnName = rawName, Value = dialog.Value });
        return true;
    }

    private bool HandleFormatTimestamp()
    {
        throw new NotImplementedException();
    }
}
