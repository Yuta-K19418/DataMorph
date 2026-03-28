using System.Diagnostics.CodeAnalysis;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.Dialogs;

/// <summary>
/// Modal dialog for formatting a timestamp column with a custom format string.
/// Allows user to specify a .NET date/time format string to reformat timestamp values.
/// </summary>
internal sealed class FormatTimestampDialog : Dialog
{
    /// <summary>
    /// Gets the target format string entered by user.
    /// </summary>
    internal string TargetFormat { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether user confirmed the action.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatTimestampDialog"/> class.
    /// </summary>
    /// <param name="columnName">The name of column to format.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by Dialog and disposed when Dialog is disposed."
    )]
    internal FormatTimestampDialog(string columnName)
    {
        throw new NotImplementedException();
    }
}
