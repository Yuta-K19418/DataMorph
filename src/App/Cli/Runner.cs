namespace DataMorph.App.Cli;

/// <summary>
/// Orchestrates the CLI headless batch processing pipeline:
/// recipe load → schema detection → output schema build → transform → write.
/// </summary>
internal static class Runner
{
    /// <summary>
    /// Runs the CLI headless batch processing pipeline.
    /// </summary>
    /// <param name="args">The validated CLI arguments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Exit code: <c>0</c> on success, <c>1</c> on any failure.</returns>
    public static ValueTask<int> RunAsync(Arguments args, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
