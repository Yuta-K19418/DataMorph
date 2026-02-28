using System.Diagnostics.CodeAnalysis;
using DataMorph.Engine.Models.Actions;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.Dialogs;

/// <summary>
/// Modal dialog for adding a row-level filter condition on a column.
/// Allows the user to select a <see cref="FilterOperator"/> and enter a comparison value.
/// </summary>
internal sealed class FilterColumnDialog : Dialog
{
    /// <summary>
    /// Gets a value indicating whether the user confirmed the filter.
    /// <see langword="false"/> if cancelled.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Gets the operator selected by the user.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal FilterOperator? SelectedOperator { get; private set; }

    /// <summary>
    /// Gets the comparison value entered by the user.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal string? Value { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterColumnDialog"/> class.
    /// </summary>
    /// <param name="columnName">The name of the column to filter on.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by the Dialog and disposed when the Dialog is disposed."
    )]
    internal FilterColumnDialog(string columnName)
    {
        Title = "Filter Column";

        var colLabel = new Label
        {
            Text = $"Column: {columnName}",
            X = 0,
            Y = 0,
        };
        var operatorLabel = new Label
        {
            Text = "Operator:",
            X = 0,
            Y = 2,
        };
        var selector = new OptionSelector<FilterOperator>
        {
            X = Pos.Right(operatorLabel) + 1,
            Y = 2,
            Width = Dim.Fill(),
            Value = FilterOperator.Equals,
        };
        var valueLabel = new Label
        {
            Text = "Value:",
            X = 0,
            Y = 4,
        };
        var textField = new TextField
        {
            Text = string.Empty,
            X = Pos.Right(valueLabel) + 1,
            Y = 4,
            Width = Dim.Fill(),
        };
        Add(colLabel, operatorLabel, selector, valueLabel, textField);

        var okButton = new Button { Text = "OK" };
        var cancelButton = new Button { Text = "Cancel" };

        void Confirm()
        {
            if (string.IsNullOrWhiteSpace(textField.Text))
            {
                return;
            }

            var value = textField.Text;
            SelectedOperator = selector.Value;
            Value = value;
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
