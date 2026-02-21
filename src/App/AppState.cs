using DataMorph.App.Schema.Csv;
using DataMorph.Engine.IO.Csv;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;
using JsonLinesIO = DataMorph.Engine.IO.JsonLines;
using JsonLinesSchema = DataMorph.App.Schema.JsonLines;

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
    /// Gets or sets the CSV row indexer for the current file.
    /// Null if no CSV file is loaded.
    /// </summary>
    public DataRowIndexer? CsvIndexer { get; set; }

    /// <summary>
    /// Gets or sets the incremental schema scanner for background schema refinement.
    /// Null if no CSV file is loaded.
    /// </summary>
    public IncrementalSchemaScanner? CsvSchemaScanner { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token source for the background schema scanner.
    /// </summary>
    public CancellationTokenSource Cts { get; set; } = new();

    /// <summary>
    /// Gets or sets the JSON Lines row indexer for the current file.
    /// Stored on load so it can be reused when switching between Tree and Table modes.
    /// </summary>
    public JsonLinesIO.RowIndexer? JsonLinesIndexer { get; set; }

    /// <summary>
    /// Gets or sets the JSON Lines schema scanner for the current file.
    /// Null until the user switches to Table mode for the first time (lazy initialization).
    /// </summary>
    public JsonLinesSchema.IncrementalSchemaScanner? JsonLinesSchemaScanner { get; set; }

    /// <summary>
    /// Gets or sets the last error message from a load operation.
    /// Null if the last operation succeeded.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the callback invoked when the background schema scan completes.
    /// Set by <c>ViewManager</c> when creating a table source that supports schema updates;
    /// invoked by <c>FileLoader</c> after background refinement finishes.
    /// </summary>
    public Action<TableSchema>? OnSchemaRefined { get; set; }

    /// <summary>
    /// Gets or sets the current Action Stack of transformation operations applied to the loaded file.
    /// An empty list means no transformations are active (passthrough).
    /// </summary>
    public IReadOnlyList<MorphAction> ActionStack { get; set; } = [];
}
