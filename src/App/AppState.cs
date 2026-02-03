using DataMorph.App.Schema;
using DataMorph.Engine.Models;

namespace DataMorph.App;

/// <summary>
/// Represents the application's global state.
/// </summary>
internal sealed class AppState
{
    /// <summary>
    /// Gets or sets the current file path being processed.
    /// </summary>
    public string CurrentFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current view mode.
    /// </summary>
    public ViewMode CurrentMode { get; set; } = ViewMode.FileSelection;

    /// <summary>
    /// Gets or sets the table schema for the loaded file.
    /// Null if no file is loaded or schema has not been detected.
    /// </summary>
    public TableSchema? Schema { get; set; }

    /// <summary>
    /// Gets or sets the incremental schema scanner for background schema refinement.
    /// Null if no CSV file is loaded.
    /// </summary>
    public IncrementalSchemaScanner? SchemaScanner { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token source for the background schema scanner.
    /// </summary>
    public CancellationTokenSource Cts { get; set; } = new();
}
