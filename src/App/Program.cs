using DataMorph.App;
using DataMorph.App.Cli;

if (args.Contains("--cli"))
{
    var parseResult = ArgumentParser.Parse(args);
    if (parseResult.IsFailure)
    {
        await Console.Error.WriteLineAsync(parseResult.Error);
        return (int)ExitCode.Failure;
    }

    var logger = new ConsoleAppLogger();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    return (int)await Runner.RunAsync(parseResult.Value, logger, cts.Token);
}

var result = TuiApplication.Create();
using var app = result.app;
using var mainWindow = result.mainWindow;

app.Init();
mainWindow.SubscribeKeyHandler();
app.Run(mainWindow);
return (int)ExitCode.Success;
