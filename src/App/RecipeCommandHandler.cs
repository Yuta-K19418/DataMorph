using System.Diagnostics.CodeAnalysis;
using DataMorph.Engine.Models;
using DataMorph.Engine.Recipes;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace DataMorph.App;

/// <summary>
/// Handles recipe save and load operations.
/// </summary>
internal sealed class RecipeCommandHandler(
    IApplication app,
    AppState state,
    ViewManager viewManager)
{
    private readonly IApplication _app = app;
    private readonly AppState _state = state;
    private readonly ViewManager _viewManager = viewManager;
    private readonly RecipeManager _recipeManager = new();

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically."
    )]
    internal async Task SaveAsync()
    {
        if (_state.CurrentMode is not (ViewMode.CsvTable or ViewMode.JsonLinesTable or ViewMode.JsonLinesTree))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return;
        }

        var dialog = new OpenDialog { Title = "Save Recipe" };
        dialog.AllowedTypes.Add(new AllowedType("YAML file", ".yaml"));

        _app.Run(dialog);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        var recipe = new Recipe
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(_state.CurrentFilePath),
            Actions = _state.ActionStack,
            LastModified = System.DateTimeOffset.UtcNow,
        };

        var result = await _recipeManager.SaveAsync(recipe, dialog.Path);

        _app.Invoke(() =>
        {
            if (result.IsFailure)
            {
                _viewManager.ShowError(result.Error);
                return;
            }

            MessageBox.Query(_app, "Save Recipe", "Recipe saved successfully.", "OK");
        });
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The OpenDialog is managed by Terminal.Gui's IApplication.Run() and will be disposed automatically."
    )]
    internal async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return;
        }

        var dialog = new OpenDialog { Title = "Load Recipe" };
        dialog.AllowedTypes.Add(new AllowedType("YAML file", ".yaml"));

        _app.Run(dialog);

        if (dialog.Canceled || string.IsNullOrEmpty(dialog.Path))
        {
            return;
        }

        await LoadFromPathAsync(dialog.Path);
    }

    internal async ValueTask LoadFromPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            throw new InvalidOperationException("Cannot load recipe: no input file is currently loaded.");
        }

        var result = await _recipeManager.LoadAsync(path);

        _app.Invoke(() =>
        {
            if (result.IsFailure)
            {
                _viewManager.ShowError(result.Error);
                return;
            }

            _state.ActionStack = result.Value.Actions;
            _viewManager.RefreshCurrentTableView();
        });
    }
}
