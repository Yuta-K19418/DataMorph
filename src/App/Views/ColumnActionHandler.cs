using DataMorph.App.Views.Dialogs;
using DataMorph.Engine.Models.Actions;
using DataMorph.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Handles column morphing actions for a table view.
/// </summary>
internal sealed class ColumnActionHandler(
    IApplication app,
    ITableSource table,
    int selectedColumn,
    Func<int, string> getRawColumnName,
    Action<MorphAction> onMorphAction,
    Func<bool>? isRowIndexComplete = null)
{
    private static readonly string[] _availableActions =
    [
        ColumnActions.Rename,
        ColumnActions.Delete,
        ColumnActions.Cast,
        ColumnActions.Filter,
        ColumnActions.Fill,
        ColumnActions.FormatTimestamp
    ];

    internal static string[] GetAvailableActions() => _availableActions;

    internal void ExecuteAction(string action)
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

    private bool HandleRenameColumn()
    {
        var rawName = getRawColumnName(selectedColumn);
        using var dialog = new RenameColumnDialog(rawName);
        app.Run(dialog);

        if (!dialog.Confirmed || dialog.NewName is null)
        {
            return true;
        }

        onMorphAction(new RenameColumnAction { OldName = rawName, NewName = dialog.NewName });
        return true;
    }

    private bool HandleDeleteColumn()
    {
        var displayName = table.ColumnNames[selectedColumn];
        var rawName = getRawColumnName(selectedColumn);
        using var dialog = new DeleteColumnDialog(displayName);
        app.Run(dialog);

        if (!dialog.Confirmed)
        {
            return true;
        }

        onMorphAction(new DeleteColumnAction { ColumnName = rawName });
        return true;
    }

    private bool HandleCastColumn()
    {
        var displayName = table.ColumnNames[selectedColumn];
        var rawName = getRawColumnName(selectedColumn);
        using var dialog = new CastColumnDialog(displayName, ColumnType.Text);
        app.Run(dialog);

        if (!dialog.Confirmed || dialog.SelectedType is null)
        {
            return true;
        }

        onMorphAction(
            new CastColumnAction { ColumnName = rawName, TargetType = dialog.SelectedType.Value }
        );
        return true;
    }

    private bool HandleFilterColumn()
    {
        if (isRowIndexComplete is not null && !isRowIndexComplete())
        {
            MessageBox.ErrorQuery(app, "Filter", "Row index is still being built. Please wait.", "OK");
            return true;
        }

        var displayName = table.ColumnNames[selectedColumn];
        var rawName = getRawColumnName(selectedColumn);
        using var dialog = new FilterColumnDialog(displayName);
        app.Run(dialog);

        if (!dialog.Confirmed || dialog.SelectedOperator is null || dialog.Value is null)
        {
            return true;
        }

        onMorphAction(
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
        var displayName = table.ColumnNames[selectedColumn];
        var rawName = getRawColumnName(selectedColumn);
        using var dialog = new FillColumnDialog(displayName);
        app.Run(dialog);

        if (!dialog.Confirmed)
        {
            return true;
        }

        onMorphAction(new FillColumnAction { ColumnName = rawName, Value = dialog.Value });
        return true;
    }

    private bool HandleFormatTimestamp()
    {
        var displayName = table.ColumnNames[selectedColumn];
        var rawName = getRawColumnName(selectedColumn);
        using var dialog = new FormatTimestampDialog(displayName);
        app.Run(dialog);

        if (!dialog.Confirmed || string.IsNullOrEmpty(dialog.TargetFormat))
        {
            return true;
        }

        onMorphAction(new FormatTimestampAction
        {
            ColumnName = rawName,
            TargetFormat = dialog.TargetFormat,
        });
        return true;
    }
}
