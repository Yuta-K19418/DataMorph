using DataMorph.App.Schema.JsonLines;
using DataMorph.Engine;
using DataMorph.Engine.IO.DrillDown;

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
        if (_state.Schema is not null)
        {
            _state.CurrentMode = ViewMode.JsonLinesTable;
            return Results.Success();
        }

        // First switch: scan schema lazily
        if (string.IsNullOrWhiteSpace(_state.CurrentFilePath))
        {
            return Results.Failure("No file is currently open");
        }

        var scanner = new IncrementalSchemaScanner(_state.CurrentFilePath);

        try
        {
            var schema = await scanner.InitialScanAsync();
            _state.Schema = schema;
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

    /// <summary>
    /// Executes the DrillDown command for the given request.
    /// Parses the selected node's bytes in memory, infers schema, and stores results in AppState.
    /// </summary>
    /// <param name="request">The DrillDown request carrying the selected node bytes and context.</param>
    /// <returns>A <see cref="Result"/> indicating success or the reason for failure.</returns>
    public ValueTask<Result> DrillDownAsync(DrillDownRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = DrillDownSchemaExtractor.ExtractFromNode(request.NodeBytes, request.Format);
        if (result.IsFailure)
        {
            return ValueTask.FromResult(Results.Failure(result.Error));
        }

        _state.DrillDown = new DrillDownState(
            result.Value.childValueBytes,
            result.Value.schema,
            request.Format,
            request.RecordPosition);
        _state.CurrentMode = ViewMode.FocusedTable;

        return ValueTask.FromResult(Results.Success());
    }
}
