namespace DataMorph.App.Cli;

/// <summary>
/// Provides a console-based implementation of the <see cref="IAppLogger"/>.
/// </summary>
internal sealed class ConsoleAppLogger : IAppLogger
{
    /// <inheritdoc/>
    public async ValueTask WriteInfoAsync(string message)
    {
        await Console.Out.WriteLineAsync(message).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask WriteWarningAsync(string message)
    {
        await Console.Error.WriteLineAsync($"Warning: {message}").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask WriteErrorAsync(string message)
    {
        await Console.Error.WriteLineAsync($"Error: {message}").ConfigureAwait(false);
    }
}
