namespace DataMorph.App.Cli;

/// <summary>
/// Provides logging capabilities for CLI operations.
/// </summary>
/// <remarks>
/// This project prioritizes Native AOT compatibility and simplicity as a CLI tool.
/// Therefore, we maintain a custom interface instead of directly using external logging libraries at this stage.
/// <br/>
/// Reasons:
/// <list type="bullet">
/// <item>
/// <description>Minimize dependencies: Introducing standard logging infrastructure increases binary size and the cost of verifying AOT behavior.</description>
/// </item>
/// <item>
/// <description>Flexibility: By using a custom interface (IAppLogger), we can easily switch to external libraries (e.g., Serilog) in the future by simply adding an adapter implementation.</description>
/// </item>
/// </list>
/// </remarks>
internal interface IAppLogger
{
    /// <summary>
    /// Writes an informational message asynchronously.
    /// </summary>
    /// <param name="message">The message to log.</param>
    ValueTask WriteInfoAsync(string message);

    /// <summary>
    /// Writes a warning message asynchronously.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    ValueTask WriteWarningAsync(string message);

    /// <summary>
    /// Writes an error message asynchronously.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    ValueTask WriteErrorAsync(string message);
}
