using System.Diagnostics.CodeAnalysis;
using DataMorph.Engine.IO.Csv;
using DataMorph.Engine.IO.JsonLines;
using DataMorph.Engine.Models;
using DataMorph.Engine.Models.Actions;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App;

/// <summary>
/// Manages the active content view inside a Terminal.Gui <see cref="Window"/>.
/// Reads Engine-layer objects from <see cref="AppState"/> and switches the visible view accordingly.
/// Has no dependency on the Engine layer directly.
/// </summary>
internal sealed class ViewManager : IDisposable
{
    private readonly Window _container;
    private readonly AppState _state;
    private readonly Func<Task> _onToggle;
    private View? _currentView;
    private bool _disposed;

    internal ViewManager(Window container, AppState state, Func<Task> onToggle)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(onToggle);
        _container = container;
        _state = state;
        _onToggle = onToggle;
    }

    /// <summary>
    /// Switches the content area to the initial file-selection prompt.
    /// </summary>
    internal void SwitchToFileSelection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SwapView(Views.FileSelectionView.Create());
    }

    /// <summary>
    /// Switches the content area to the virtualized CSV table view.
    /// Wraps the source with <see cref="Views.LazyTransformer"/> when the Action Stack is non-empty.
    /// </summary>
    /// <param name="indexer">The CSV row indexer for the loaded file.</param>
    /// <param name="schema">The detected table schema.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by the container and disposed via SwapView."
    )]
    internal void SwitchToCsvTable(DataRowIndexer indexer, TableSchema schema)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(schema);

        ITableSource rawSource = new Views.VirtualTableSource(indexer, schema);
        var source =
            _state.ActionStack.Count == 0
                ? rawSource
                : new Views.LazyTransformer(rawSource, schema, _state.ActionStack);

        var view = new Views.CsvTableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = source,
            Style = new TableStyle { AlwaysShowHeaders = true },
            OnMorphAction = HandleMorphAction,
        };
        SwapView(view);
    }

    /// <summary>
    /// Switches the content area to the JSON Lines hierarchical tree view.
    /// </summary>
    /// <param name="indexer">The JSON Lines row indexer for the loaded file.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by the container and disposed via SwapView."
    )]
    internal void SwitchToJsonLinesTree(RowIndexer indexer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(indexer);

        var view = new Views.JsonLinesTreeView(indexer, () => _ = _onToggle())
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        SwapView(view);
    }

    /// <summary>
    /// Switches the content area to the JSON Lines table view.
    /// Wraps the source with <see cref="Views.LazyTransformer"/> when the Action Stack is non-empty.
    /// Registers <see cref="AppState.OnSchemaRefined"/> for background schema updates.
    /// </summary>
    /// <param name="indexer">The JSON Lines row indexer for the loaded file.</param>
    /// <param name="schema">The detected table schema.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by the container and disposed via SwapView."
    )]
    internal void SwitchToJsonLinesTableView(RowIndexer indexer, TableSchema schema)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(schema);

        var cache = new RowByteCache(indexer);
        var source = new Views.JsonLinesTableSource(cache, schema);
        _state.OnSchemaRefined = source.UpdateSchema;

        ITableSource tableSource =
            _state.ActionStack.Count == 0
                ? source
                : new Views.LazyTransformer(source, schema, _state.ActionStack);

        var view = new Views.JsonLinesTableView(() => _ = _onToggle(), HandleMorphAction)
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Table = tableSource,
            Style = new TableStyle { AlwaysShowHeaders = true },
        };
        SwapView(view);
    }

    /// <summary>
    /// Refreshes the current table view by re-invoking the appropriate <c>SwitchTo*</c> method.
    /// Called after a morph action is added to <see cref="AppState.ActionStack"/> so that
    /// <see cref="Views.LazyTransformer"/> is reconstructed with the updated stack.
    /// </summary>
    internal void RefreshCurrentTableView()
    {
        switch (_state.CurrentMode)
        {
            case ViewMode.CsvTable when _state.CsvIndexer is not null && _state.Schema is not null:
                SwitchToCsvTable(_state.CsvIndexer, _state.Schema);
                break;

            case ViewMode.JsonLinesTable
                when _state.JsonLinesIndexer is not null && _state.Schema is not null:
                SwitchToJsonLinesTableView(_state.JsonLinesIndexer, _state.Schema);
                break;
        }
    }

    /// <summary>
    /// Handles a column morphing action from a table view.
    /// Appends the action to the stack and refreshes the current view.
    /// </summary>
    /// <param name="action">The morph action to apply.</param>
    private void HandleMorphAction(MorphAction action)
    {
        _state.AddMorphAction(action);
        RefreshCurrentTableView();
    }

    /// <summary>
    /// Displays an error message in a placeholder view.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by the container and disposed via SwapView."
    )]
    internal void ShowError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(message);

        _state.CurrentMode = ViewMode.PlaceholderView;
        var view = Views.PlaceholderView.Create(_state);
        view.Text = message;
        SwapView(view);
    }

    private void SwapView(View newView)
    {
        if (_currentView is not null)
        {
            _container.Remove(_currentView);
            _currentView.Dispose();
        }
        _currentView = newView;
        _container.Add(_currentView);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _currentView?.Dispose();
    }
}
