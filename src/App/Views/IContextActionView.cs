namespace DataMorph.App.Views;

/// <summary>
/// Defines a view that can provide context-sensitive actions for the action menu.
/// </summary>
internal interface IContextActionView
{
    /// <summary>
    /// Gets the list of available actions for the current selection.
    /// </summary>
    /// <returns>An array of action names.</returns>
    string[] GetAvailableActions();

    /// <summary>
    /// Executes the specified action.
    /// </summary>
    /// <param name="action">The name of the action to execute.</param>
    void ExecuteAction(string action);
}
