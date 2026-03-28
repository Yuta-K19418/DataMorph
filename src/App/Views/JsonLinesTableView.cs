using DataMorph.App.Views.Dialogs;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// A TableView for JSON Lines data that intercepts the 't' key to allow switching back to
/// tree mode, and adds vim-like key navigation (h/j/k/l, gg, Shift+G).
/// </summary>
internal sealed class JsonLinesTableView : TableView
{
    private readonly Action _onTableModeToggle;
    private readonly Action<MorphAction>? _onMorphAction;
    private readonly Func<bool>? _isRowIndexComplete;
    private readonly Func<int, string>? _getRawColumnName;
    private readonly VimKeyTranslator _vimKeys = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLinesTableView"/> class.
    /// </summary>
    /// <param name="onTableModeToggle">Callback invoked when the user presses 't'.</param>
    /// <param name="onMorphAction">
    /// Callback invoked when the user confirms a column morphing action.
    /// <see langword="null"/> disables morphing for this view instance.
    /// </param>
    /// <param name="isRowIndexComplete">
    /// Optional predicate that returns <see langword="true"/> when the row indexer's
    /// <c>BuildIndex</c> has completed. When <see langword="null"/>, the guard is skipped.
    /// The <c>Shift+F</c> filter action is blocked until this returns <see langword="true"/>.
    /// </param>
    /// <param name="getRawColumnName">
    /// Optional delegate that resolves a column index to its raw (unlabeled) schema name.
    /// Used when constructing <see cref="MorphAction"/>s to avoid embedding type labels in
    /// action column names. When <see langword="null"/>, morphing actions are disabled.
    /// </param>
    internal JsonLinesTableView(
        Action onTableModeToggle,
        Action<MorphAction>? onMorphAction = null,
        Func<bool>? isRowIndexComplete = null,
        Func<int, string>? getRawColumnName = null
    )
    {
        _onTableModeToggle = onTableModeToggle;
        _onMorphAction = onMorphAction;
        _isRowIndexComplete = isRowIndexComplete;
        _getRawColumnName = getRawColumnName;
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (Table is null)
        {
            throw new InvalidOperationException("Table cannot be null");
        }

        if (key.KeyCode == KeyCode.T)
        {
            _onTableModeToggle();
            return true;
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
        if (App is null || _onMorphAction is null || _getRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var rawName = _getRawColumnName(SelectedColumn);
        using var dialog = new RenameColumnDialog(rawName);
        App.Run(dialog);

        if (!dialog.Confirmed || dialog.NewName is null)
        {
            return true;
        }

        _onMorphAction(new RenameColumnAction { OldName = rawName, NewName = dialog.NewName });
        return true;
    }

    private bool HandleDeleteColumn()
    {
        if (App is null || _onMorphAction is null || _getRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = _getRawColumnName(SelectedColumn);
        using var dialog = new DeleteColumnDialog(displayName);
        App.Run(dialog);

        if (!dialog.Confirmed)
        {
            return true;
        }

        _onMorphAction(new DeleteColumnAction { ColumnName = rawName });
        return true;
    }

    private bool HandleCastColumn()
    {
        if (App is null || _onMorphAction is null || _getRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = _getRawColumnName(SelectedColumn);
        using var dialog = new CastColumnDialog(displayName, ColumnType.Text);
        App.Run(dialog);

        if (!dialog.Confirmed || dialog.SelectedType is null)
        {
            return true;
        }

        _onMorphAction(
            new CastColumnAction { ColumnName = rawName, TargetType = dialog.SelectedType.Value }
        );
        return true;
    }

    private bool HandleFilterColumn()
    {
        if (App is null || _onMorphAction is null || _getRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        if (_isRowIndexComplete is not null && !_isRowIndexComplete())
        {
            MessageBox.ErrorQuery(App, "Filter", "Row index is still being built. Please wait.", "OK");
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = _getRawColumnName(SelectedColumn);
        using var dialog = new FilterColumnDialog(displayName);
        App.Run(dialog);

        if (!dialog.Confirmed || dialog.SelectedOperator is null || dialog.Value is null)
        {
            return true;
        }

        _onMorphAction(
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
        if (App is null || _onMorphAction is null || _getRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = _getRawColumnName(SelectedColumn);
        using var dialog = new FillColumnDialog(displayName);
        App.Run(dialog);

        if (!dialog.Confirmed)
        {
            return true;
        }

        _onMorphAction(new FillColumnAction { ColumnName = rawName, Value = dialog.Value });
        return true;
    }

    private bool HandleFormatTimestamp()
    {
        if (App is null || _onMorphAction is null || _getRawColumnName is null
            || Table is null || SelectedColumn < 0)
        {
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = _getRawColumnName(SelectedColumn);
        using var dialog = new FormatTimestampDialog(displayName);
        App.Run(dialog);

        if (!dialog.Confirmed || string.IsNullOrEmpty(dialog.TargetFormat))
        {
            return true;
        }

        _onMorphAction(new FormatTimestampAction
        {
            ColumnName = rawName,
            TargetFormat = dialog.TargetFormat,
        });
        return true;
    }
}
