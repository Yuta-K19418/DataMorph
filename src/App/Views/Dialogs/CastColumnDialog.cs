using System.Diagnostics.CodeAnalysis;
using DataMorph.Engine.Types;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.Dialogs;

/// <summary>
/// Modal dialog for casting a column to a different data type.
/// Displays all available <see cref="ColumnType"/> values as radio options.
/// </summary>
internal sealed class CastColumnDialog : Dialog
{
    /// <summary>
    /// Gets the column type selected by the user.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal ColumnType? SelectedType { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user confirmed the cast.
    /// <see langword="false"/> if cancelled or the same type was selected.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CastColumnDialog"/> class.
    /// </summary>
    /// <param name="columnName">The name of the column to cast.</param>
    /// <param name="currentType">The current column type, pre-selected in the option list.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed."
    )]
    internal CastColumnDialog(string columnName, ColumnType currentType)
    {
        Title = "Cast Column Type";

        var colLabel = new Label
        {
            Text = $"Column: {columnName}",
            X = 0,
            Y = 0,
        };
        var typeLabel = new Label
        {
            Text = $"Current type: {currentType}",
            X = 0,
            Y = 1,
        };
        var selector = new OptionSelector<ColumnType>
        {
            X = 0,
            Y = 3,
            Width = Dim.Fill(),
            Value = currentType,
        };
        Add(colLabel, typeLabel, selector);

        var okButton = new Button { Text = "OK" };
        var cancelButton = new Button { Text = "Cancel" };

        void Confirm()
        {
            var selected = selector.Value;
            if (selected.Value == currentType)
            {
                return;
            }

            SelectedType = selected;
            Confirmed = true;
            App?.RequestStop();
        }

        okButton.Accepting += (sender, e) =>
        {
            e.Handled = true;
            Confirm();
        };

        selector.Accepting += (sender, e) =>
        {
            e.Handled = true;
            Confirm();
        };

        AddButton(okButton);
        AddButton(cancelButton);
    }
}
