using DataMorph.Engine;
using JsonLinesSchema = DataMorph.App.Schema.JsonLines;

namespace DataMorph.App;

/// <summary>
/// Orchestrates view mode transitions and associated lazy initialization logic.
/// </summary>
internal sealed class ModeController
{
    private readonly AppState _state;

    public ModeController(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>
    /// Toggles the JSON Lines display mode between Tree and Table.
    /// Performs a lazy schema scan on the first switch to Table mode.
    /// </summary>
    /// <returns>A <see cref="Result"/> indicating success or the reason for failure.</returns>
    public async ValueTask<Result> ToggleJsonLinesModeAsync()
    {
        if (_state.CurrentMode == ViewMode.JsonLinesTable)
        {
            _state.CurrentMode = ViewMode.JsonLinesTree;
            return Results.Success();
        }

        if (_state.CurrentMode != ViewMode.JsonLinesTree)
        {
            return Results.Success();
        }

        if (_state.RowIndexer is null)
        {
            return Results.Success();
        }

        // Subsequent switch: reuse cached schema
        if (_state.JsonLinesSchemaScanner is not null && _state.Schema is not null)
        {
            _state.CurrentMode = ViewMode.JsonLinesTable;
            return Results.Success();
        }

        // First switch: scan schema lazily
        if (string.IsNullOrEmpty(_state.CurrentFilePath))
        {
            return Results.Failure("No file is currently open");
        }

        var scanner = new JsonLinesSchema.IncrementalSchemaScanner(_state.CurrentFilePath);

        try
        {
            var schema = await scanner.InitialScanAsync();
            _state.Schema = schema;
            _state.JsonLinesSchemaScanner = scanner;
            _state.CurrentMode = ViewMode.JsonLinesTable;

            _ = scanner
                .StartBackgroundScanAsync(schema, _state.Cts.Token)
                .ContinueWith(
                    t =>
                    {
                        if (!t.IsCompletedSuccessfully)
                        {
                            return;
                        }

                        _state.Schema = t.Result;
                        _state.OnSchemaRefined?.Invoke(t.Result);
                    },
                    TaskScheduler.Default
                );

            return Results.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Results.Failure(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            return Results.Failure($"Invalid JSON Lines format: {ex.Message}");
        }
    }
}
