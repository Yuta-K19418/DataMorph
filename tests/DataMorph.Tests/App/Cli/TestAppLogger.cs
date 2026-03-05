namespace DataMorph.Tests.App.Cli;

/// <summary>
/// Captures log messages for testing purposes.
/// </summary>
internal sealed class TestAppLogger : DataMorph.App.Cli.IAppLogger
{
    private readonly List<string> _infos = [];
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Infos => _infos.AsReadOnly();
    public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    public int LogCount => _infos.Count + _warnings.Count + _errors.Count;

    public void Clear()
    {
        _infos.Clear();
        _warnings.Clear();
        _errors.Clear();
    }

    /// <inheritdoc/>
    public ValueTask WriteInfoAsync(string message)
    {
        _infos.Add(message);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask WriteWarningAsync(string message)
    {
        _warnings.Add(message);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask WriteErrorAsync(string message)
    {
        _errors.Add(message);
        return ValueTask.CompletedTask;
    }
}
