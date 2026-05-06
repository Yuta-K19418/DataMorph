using DataMorph.Engine.Models.Actions;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App.Views;

/// <summary>
/// Base class for table views that support column morph actions.
/// Provides common implementation for vim-like navigation.
/// </summary>
internal abstract class MorphTableView : TableView
{
    private readonly VimKeyTranslator _vimKeys = new();
    private int[] _maxColumnWidths = [];

    /// <summary>
    /// Callback invoked when the user confirms a column morphing action.
    /// <see langword="null"/> means morphing is disabled for this view instance.
    /// </summary>
    internal Action<MorphAction>? OnMorphAction { get; init; }

    /// <summary>
    /// Optional predicate that returns <see langword="true"/> when the row indexer's
    /// <c>BuildIndex</c> has completed. When <see langword="null"/>, the guard is skipped.
    /// The filter action is blocked until this returns <see langword="true"/>.
    /// </summary>
    internal Func<bool>? IsRowIndexComplete { get; init; }

    /// <summary>
    /// Resolves a column index to the raw (un-labeled) column name for action creation.
    /// When <see langword="null"/>, morphing is disabled (same guard as <see cref="OnMorphAction"/>).
    /// </summary>
    internal Func<int, string>? GetRawColumnName { get; init; }

    /// <summary>
    /// Initializes column widths from header text lengths and installs per-column
    /// <see cref="ColumnStyle.RepresentationGetter"/> callbacks to track the maximum
    /// cell width observed during rendering. Widths grow only, never shrink.
    /// </summary>
    internal void InitializeColumnWidths()
    {
        if (Table is null)
        {
            return;
        }

        _maxColumnWidths = new int[Table.Columns];
        for (var col = 0; col < Table.Columns; col++)
        {
            var colIdx = col;
            _maxColumnWidths[colIdx] = Table.ColumnNames[colIdx].Length;
            var colStyle = Style.GetOrCreateColumnStyle(colIdx);
            colStyle.MinWidth = _maxColumnWidths[colIdx];
            colStyle.RepresentationGetter = value =>
            {
                var text = value?.ToString() ?? string.Empty;
                if (text.Length > _maxColumnWidths[colIdx])
                {
                    _maxColumnWidths[colIdx] = text.Length;
                }

                return text;
            };
        }
    }

    /// <inheritdoc/>
    protected override bool OnDrawingContent(DrawContext? ctx)
    {
        var result = base.OnDrawingContent(ctx);
        if (_maxColumnWidths.Length == 0)
        {
            return result;
        }

        var changed = false;
        for (var col = 0; col < _maxColumnWidths.Length; col++)
        {
            var colStyle = Style.GetOrCreateColumnStyle(col);
            if (colStyle.MinWidth != _maxColumnWidths[col])
            {
                colStyle.MinWidth = _maxColumnWidths[col];
                changed = true;
            }
        }

        if (changed)
        {
            RefreshContentSize();
        }

        return result;
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        if (Table is null)
        {
            return base.OnKeyDown(key);
        }

        var action = _vimKeys.Translate(key.KeyCode);

        void moveToRow(int row)
        {
            // Cannot use Command.Start/End as they reset the column to 0 or rightmost.
            // We need to preserve the current column while moving rows.
            if (Value is null)
            {
                return;
            }

            SetSelection(col: Value.SelectedCell.X, row: row, extendExistingSelection: false);
            Update();
            SetNeedsDraw();
        }

        static bool execute(Action a)
        {
            a();
            return true;
        }

        return action switch
        {
            VimAction.MoveDown => execute(() => InvokeCommand(Command.Down)),
            VimAction.MoveUp => execute(() => InvokeCommand(Command.Up)),
            VimAction.MoveLeft => execute(() => InvokeCommand(Command.Left)),
            VimAction.MoveRight => execute(() => InvokeCommand(Command.Right)),
            VimAction.PageDown => execute(() => InvokeCommand(Command.PageDown)),
            VimAction.PageUp => execute(() => InvokeCommand(Command.PageUp)),
            VimAction.GoToFirst => execute(() => moveToRow(0)),
            VimAction.GoToEnd => execute(() => moveToRow(Table.Rows - 1)),
            VimAction.PendingGSequence => true,
            _ => HandleNonVimKey(key),
        };
    }

    private bool HandleNonVimKey(Key key)
    {
        // Prevent global shortcut keys from being consumed by TableView's incremental search.
        // By returning false, we let these keys bubble up to AppKeyHandler.
        if (AppKeyHandler.IsGlobalShortcut(key.KeyCode))
        {
            return false;
        }

        return base.OnKeyDown(key);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Idiomatic safe disposal sequence:
            // Unbind data source
            IDisposable? tableToDispose = null;
            if (Table is IDisposable d)
            {
                tableToDispose = d;
            }
            Table = null;

            // Clear selection state (critical to prevent RenderRow crash)
            Value = null;

            // Mark for redraw
            SetNeedsDraw();

            // Dispose data source safely
            tableToDispose?.Dispose();
        }

        base.Dispose(disposing);
    }
}
