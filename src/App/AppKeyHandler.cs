using System.Diagnostics.CodeAnalysis;
using DataMorph.App.Views;
using DataMorph.App.Views.Dialogs;
using DataMorph.Engine.Types;
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace DataMorph.App;

/// <summary>
/// Handles global keyboard shortcuts for DataMorph application.
/// </summary>
internal sealed class AppKeyHandler : IDisposable
{
    private readonly IApplication _app;
    private readonly AppState _state;
    private readonly ViewManager _viewManager;
    private readonly FileDialogHandler _fileDialogHandler;
    private readonly RecipeCommandHandler _recipeCommandHandler;

    [SuppressMessage(
        "Reliability",
        "CA2213:Disposable fields should be disposed",
        Justification = "Child views added to the Window will be disposed automatically when the Window is disposed."
    )]
    private readonly StatusBar? _statusBar;

    private bool _disposed;

    /// <summary>
    /// Determines whether the specified key code corresponds to a global application shortcut.
    /// </summary>
    /// <param name="keyCode">The key code to check.</param>
    /// <returns><c>true</c> if the key is a global shortcut; <c>false</c> otherwise.</returns>
    internal static bool IsGlobalShortcut(KeyCode keyCode)
    {
        var baseKey = keyCode & ~(KeyCode.ShiftMask | KeyCode.CtrlMask | KeyCode.AltMask);
        return baseKey is KeyCode.O or KeyCode.S or KeyCode.Q or KeyCode.T or KeyCode.X or (KeyCode)'?';
    }

    internal AppKeyHandler(
        IApplication app,
        AppState state,
        ViewManager viewManager,
        FileDialogHandler fileDialogHandler,
        RecipeCommandHandler recipeCommandHandler,
        StatusBar? statusBar
    )
    {
        _app = app;
        _state = state;
        _viewManager = viewManager;
        _fileDialogHandler = fileDialogHandler;
        _recipeCommandHandler = recipeCommandHandler;
        _statusBar = statusBar;
    }

    internal void Subscribe()
    {
        _app.Keyboard.KeyDown -= OnGlobalKeyDown;
        _app.Keyboard.KeyDown += OnGlobalKeyDown;
    }

    /// <summary>
    /// Handles quit shortcut (q).
    /// Confirms with user if there are unsaved changes.
    /// </summary>
    /// <returns><c>true</c> if the key was handled; <c>false</c> otherwise.</returns>
    private bool HandleQuit()
    {
        if (_state.ActionStack.Count == 0)
        {
            _app.RequestStop();
            return true;
        }

        var result = MessageBox.Query(
            _app,
            "Quit",
            "You have unsaved changes in your recipe. Quit anyway?",
            "Yes",
            "No"
        );
        if (result == 0)
        {
            _app.RequestStop();
        }

        return true;
    }

    /// <summary>
    /// Handles help overlay shortcut (?).
    /// </summary>
    /// <returns><c>true</c> if the key was handled; <c>false</c> otherwise.</returns>
    private bool HandleHelp()
    {
        using var dialog = new HelpDialog();
        _app.Run(dialog);
        return true;
    }

    private bool HandleOpen()
    {
        _ = _fileDialogHandler.ShowAsync().ContinueWith(
            t =>
            {
                if (t.IsFaulted && t.Exception is not null)
                {
                    _app.Invoke(() => _viewManager.ShowError(t.Exception.InnerException?.Message ?? t.Exception.Message));
                }
            },
            TaskScheduler.Default
        );
        return true;
    }

    private bool HandleSave()
    {
        _ = _recipeCommandHandler.SaveAsync().ContinueWith(
            t =>
            {
                if (t.IsFaulted && t.Exception is not null)
                {
                    _app.Invoke(() => _viewManager.ShowError(t.Exception.InnerException?.Message ?? t.Exception.Message));
                }
            },
            TaskScheduler.Default
        );
        return true;
    }

    private bool HandleViewToggle()
    {
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return false;
        }

        var format = FormatDetector.Detect(_state.CurrentFilePath);
        if (format.IsSuccess && format.Value == DataFormat.JsonLines)
        {
            _ = _viewManager.ToggleJsonLinesModeAsync().ContinueWith(
                t =>
                {
                    if (t.IsFaulted && t.Exception is not null)
                    {
                        _app.Invoke(() => _viewManager.ShowError(t.Exception.InnerException?.Message ?? t.Exception.Message));
                    }
                },
                TaskScheduler.Default
            );
            return true;
        }

        return false;
    }

    internal bool HandleActionMenu()
    {
        if (_viewManager.GetCurrentView() is not MorphTableView mt)
        {
            return false;
        }

        if (mt.Table is null || mt.GetRawColumnName is null
            || mt.OnMorphAction is null || mt.SelectedColumn < 0)
        {
            return false;
        }

        var handler = new ColumnActionHandler(
            _app, mt.Table, mt.SelectedColumn,
            mt.GetRawColumnName, mt.OnMorphAction, mt.IsRowIndexComplete);

        using var dialog = new ActionMenuDialog(ColumnActionHandler.GetAvailableActions(), handler.ExecuteAction);
        _app.Run(dialog);
        return true;
    }

    private void OnGlobalKeyDown(object? sender, Key key)
    {
        if (key.Handled)
        {
            return;
        }

        // Skip global key handling when a text input view is focused
        var focused = _app.Navigation?.GetFocused() ?? _app.TopRunnableView?.MostFocused;
        var current = focused;
        while (current is not null)
        {
            var type = current.GetType();
            if (current is TextField || type.Name == "TextField" || type.FullName == "Terminal.Gui.Views.TextField")
            {
                return;
            }
            current = current.SuperView;
        }

        // Shortcuts like o, s, q, t, x should not have Ctrl or Alt modifiers.
        if ((key.KeyCode & (KeyCode.CtrlMask | KeyCode.AltMask)) != 0)
        {
            return;
        }

        var baseKey = key.KeyCode & ~KeyCode.ShiftMask;
        key.Handled = baseKey switch
        {
            KeyCode.O => HandleOpen(),
            KeyCode.S => HandleSave(),
            KeyCode.Q => HandleQuit(),
            KeyCode.T => HandleViewToggle(),
            KeyCode.X => HandleActionMenu(),
            (KeyCode)'?' => HandleHelp(),
            _ => false,
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _app.Keyboard.KeyDown -= OnGlobalKeyDown;
        _disposed = true;
    }
}
