using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.Dialogs;

/// <summary>
/// Modal dialog for context-sensitive action menu.
/// Allows users to discover and execute actions available for the current selection.
/// </summary>
internal sealed class ActionMenuDialog : Dialog
{
    [SuppressMessage(
        "Reliability",
        "CA2213:Disposable fields should be disposed",
        Justification = "Child views added to Dialog will be disposed automatically when the Dialog is disposed."
    )]
    private readonly ListView _listView;

    /// <summary>
    /// Gets the index of the currently selected item in the list.
    /// </summary>
    internal int SelectedItemIndex => _listView.SelectedItem ?? -1;

    /// <summary>
    /// Simulates a key press on the list view. Used in tests to drive navigation.
    /// </summary>
    internal bool SimulateListKeyDown(Key key) => _listView.NewKeyDownEvent(key);

    /// <summary>
    /// Gets the selected action from the menu.
    /// <see langword="null"/> if the dialog was cancelled.
    /// </summary>
    internal string? SelectedAction { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the user confirmed an action selection.
    /// <see langword="false"/> if the dialog was cancelled.
    /// </summary>
    internal bool Confirmed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionMenuDialog"/> class.
    /// </summary>
    /// <param name="availableActions">List of actions available for the current context.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by Dialog and disposed when Dialog is disposed."
    )]
    internal ActionMenuDialog(string[] availableActions)
    {
        ArgumentNullException.ThrowIfNull(availableActions);

        Title = "Actions";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(50);
        Height = Dim.Percent(50);

        var collection = new ObservableCollection<string>(availableActions);
        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            Source = new ListWrapper<string>(collection),
        };

        if (collection.Count > 0)
        {
            _listView.SelectedItem = 0;
        }

        _listView.KeyBindings.Add(Key.J, Command.Down);
        _listView.KeyBindings.Add(Key.K, Command.Up);

        _listView.Accepting += (sender, e) => ExecuteSelectedAction();

        Add(_listView);

        // Add cancel button
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (sender, e) =>
        {
            e.Handled = true;
            App?.RequestStop();
        };

        AddButton(cancelButton);
    }

    /// <summary>
    /// Executes the currently selected action and closes the dialog.
    /// </summary>
    private void ExecuteSelectedAction()
    {
        if (_listView.SelectedItem is { } selectedIndex && selectedIndex >= 0)
        {
            var items = _listView.Source?.ToList();
            if (items is not null && selectedIndex < items.Count)
            {
                SelectedAction = items[selectedIndex]?.ToString()
                    ?? throw new UnreachableException("List item must not be null");
                Confirmed = true;
            }
        }

        App?.RequestStop();
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        var baseKey = (char)(key.KeyCode & KeyCode.CharMask);
        var baseKeyLower = char.ToLowerInvariant(baseKey);

        if (key.KeyCode == KeyCode.Esc || baseKeyLower == 'x')
        {
            App?.RequestStop();
            return true;
        }

        return base.OnKeyDown(key);
    }
}
