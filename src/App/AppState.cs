using DataMorph.Engine.IO;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;

namespace DataMorph.App;

/// <summary>
/// Represents the application's global state.
/// </summary>
internal sealed class AppState : IDisposable
{
    private bool _disposed;

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
    /// Gets or sets the row indexer for the current file.
    /// Stored on load so it can be reused when switching modes.
    /// </summary>
    public IRowIndexer? RowIndexer { get; set; }

    /// <summary>
    /// Gets or sets the cancellation token source for the background schema scanner.
    /// </summary>
    public CancellationTokenSource Cts { get; private set; } = new();

    /// <summary>
    /// Gets or sets the callback invoked when the background schema scan completes.
    /// Set by <c>ViewManager</c> when creating a table source that supports schema updates;
    /// invoked after background refinement finishes.
    /// </summary>
    public Action<TableSchema>? OnSchemaRefined { get; set; }

    /// <summary>
    /// Gets or sets the current Action Stack of transformation operations applied to the loaded file.
    /// An empty list means no transformations are active (passthrough).
    /// </summary>
    public IReadOnlyList<MorphAction> ActionStack { get; set; } = [];

    /// <summary>
    /// Renews the cancellation token source by cancelling the current one and creating a new one.
    /// This should be called when loading a new file to ensure the previous file's background scan
    /// is cancelled and does not interfere with the new file.
    /// </summary>
    public void RenewCtsWithCancel()
    {
        // Cancel must precede Dispose: any captured CancellationToken derived from the old Cts
        // will reflect IsCancellationRequested = true, keeping polling-based checks safe after disposal.
        Cts.Cancel();
        Cts.Dispose();
        Cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Appends a morph action to the Action Stack.
    /// Creates a new <see cref="IReadOnlyList{T}"/> to preserve immutability.
    /// </summary>
    /// <param name="action">The action to append.</param>
    internal void AddMorphAction(MorphAction action)
    {
        ActionStack = [.. ActionStack, action];
    }

    /// <summary>
    /// Clears all morph actions from the Action Stack, resetting it to an empty state.
    /// </summary>
    internal void ClearMorphActions()
    {
        ActionStack = [];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Cts.Cancel();
        Cts.Dispose();
        _disposed = true;
    }
}
