using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// A TableView for JSON Lines data that intercepts the 't' key
/// to allow switching back to tree mode.
/// </summary>
internal sealed class JsonLinesTableView : TableView
{
    private readonly Action _onTableModeToggle;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonLinesTableView"/> class.
    /// </summary>
    /// <param name="onTableModeToggle">Callback invoked when the user presses 't'.</param>
    internal JsonLinesTableView(Action onTableModeToggle)
    {
        _onTableModeToggle = onTableModeToggle;
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (key.KeyCode == KeyCode.T)
        {
            _onTableModeToggle();
            return true;
        }

        return base.OnKeyDown(key);
    }
}
