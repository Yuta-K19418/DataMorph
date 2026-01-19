namespace DataMorph.App;

/// <summary>
/// Defines the available view modes in the TUI application.
/// </summary>
internal enum ViewMode
{
    /// <summary>
    /// File selection dialog view.
    /// </summary>
    FileSelection,

    /// <summary>
    /// Placeholder view displaying loaded file information.
    /// Will be replaced by CsvTable, JsonTable views in future issues.
    /// </summary>
    PlaceholderView
}
