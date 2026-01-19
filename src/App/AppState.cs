namespace DataMorph.App;

/// <summary>
/// Manages application state for the TUI application.
/// </summary>
internal sealed class AppState
{
    /// <summary>
    /// Gets or sets the current file path.
    /// Empty string if no file is selected.
    /// </summary>
    public string CurrentFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current view mode.
    /// </summary>
    public ViewMode CurrentMode { get; set; } = ViewMode.FileSelection;
}
