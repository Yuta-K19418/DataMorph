using System.Diagnostics.CodeAnalysis;
using DataMorph.App.Views.Dialogs;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Base class for table views that support column morph actions.
/// Provides common implementation for action menu handling and vim-like navigation.
/// </summary>
internal abstract class MorphTableView : TableView, IContextActionView
{
    private readonly VimKeyTranslator _vimKeys = new();

    [MemberNotNullWhen(true, nameof(OnMorphAction), nameof(GetRawColumnName))]
    private bool IsActionReady() =>
        App is not null && Table is not null && OnMorphAction is not null && GetRawColumnName is not null
        && SelectedColumn >= 0;

    /// <summary>
    /// Callback invoked when the user confirms a column morphing action.
    /// <see langword="null"/> means morphing is disabled for this view instance.
    /// </summary>
    internal Action<MorphAction>? OnMorphAction { get; init; }

    /// <summary>
    /// Optional predicate that returns <see langword="true"/> when the row indexer's
    /// <c>BuildIndex</c> has completed. When <see langword="null"/>, the guard is skipped.
    /// The filter action is blocked until this returns <see langword="true"/>.
    /// </summary>
    internal Func<bool>? IsRowIndexComplete { get; init; }

    /// <summary>
    /// Resolves a column index to the raw (un-labeled) column name for action creation.
    /// When <see langword="null"/>, morphing is disabled (same guard as <see cref="OnMorphAction"/>).
    /// </summary>
    internal Func<int, string>? GetRawColumnName { get; init; }

    /// <inheritdoc/>
    public string[] GetAvailableActions()
    {
        if (OnMorphAction is null || GetRawColumnName is null || Table is null || SelectedColumn < 0)
        {
            return [];
        }

        return [
            ColumnActions.Rename,
            ColumnActions.Delete,
            ColumnActions.Cast,
            ColumnActions.Filter,
            ColumnActions.Fill,
            ColumnActions.FormatTimestamp
        ];
    }

    /// <inheritdoc/>
    public void ExecuteAction(string action)
    {
        _ = action switch
        {
            ColumnActions.Rename => HandleRenameColumn(),
            ColumnActions.Delete => HandleDeleteColumn(),
            ColumnActions.Cast => HandleCastColumn(),
            ColumnActions.Filter => HandleFilterColumn(),
            ColumnActions.Fill => HandleFillColumn(),
            ColumnActions.FormatTimestamp => HandleFormatTimestamp(),
            _ => false,
        };
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (Table is null)
        {
            return base.OnKeyDown(key);
        }

        var action = _vimKeys.Translate(key.KeyCode);

        static bool Do(Action a)
        { a(); return true; }

        return action switch
        {
            VimAction.MoveDown => Do(() => ChangeSelectionByOffset(0, 1, false)),
            VimAction.MoveUp => Do(() => ChangeSelectionByOffset(0, -1, false)),
            VimAction.MoveLeft => Do(() => ChangeSelectionByOffset(-1, 0, false)),
            VimAction.MoveRight => Do(() => ChangeSelectionByOffset(1, 0, false)),
            VimAction.PageDown => Do(() => ChangeSelectionByOffset(0, Viewport.Height, false)),
            VimAction.PageUp => Do(() => ChangeSelectionByOffset(0, -Viewport.Height, false)),
            VimAction.GoToFirst => Do(() => ChangeSelectionByOffset(0, -SelectedRow, false)),
            VimAction.GoToEnd => Do(() => ChangeSelectionByOffset(0, Table.Rows - 1 - SelectedRow, false)),
            VimAction.PendingGSequence => true,
            _ => HandleNonVimKey(key),
        };
    }

    private bool HandleNonVimKey(Key key)
    {
        // Prevent global shortcut keys from being consumed by TableView's incremental search.
        // By returning false, we let these keys bubble up to AppKeyHandler.
        if (AppKeyHandler.IsGlobalShortcut(key.KeyCode))
        {
            return false;
        }

        return base.OnKeyDown(key);
    }

    private bool HandleRenameColumn()
    {
        if (!IsActionReady() || App is null || Table is null)
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
        if (!IsActionReady() || App is null || Table is null)
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
        if (!IsActionReady() || App is null || Table is null)
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
        if (!IsActionReady() || App is null || Table is null)
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
        if (!IsActionReady() || App is null || Table is null)
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
        if (!IsActionReady() || App is null || Table is null)
        {
            return true;
        }

        var displayName = Table.ColumnNames[SelectedColumn];
        var rawName = GetRawColumnName(SelectedColumn);
        using var dialog = new FormatTimestampDialog(displayName);
        App.Run(dialog);

        if (!dialog.Confirmed || string.IsNullOrEmpty(dialog.TargetFormat))
        {
            return true;
        }

        OnMorphAction(new FormatTimestampAction
        {
            ColumnName = rawName,
            TargetFormat = dialog.TargetFormat,
        });
        return true;
    }
}
