using DataMorph.Engine.Models.Actions;
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
    internal FilterColumnDialog(string columnName)
    {
        throw new NotImplementedException();
    }
}
