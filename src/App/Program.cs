using DataMorph.App;
using DataMorph.App.Cli;

if (args.Contains("--cli"))
{
    var parseResult = ArgumentParser.Parse(args);
    if (parseResult.IsFailure)
    {
        await Console.Error.WriteLineAsync(parseResult.Error);
        return 1;
    }

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    return await Runner.RunAsync(parseResult.Value, cts.Token);
}

var result = TuiApplication.Create();
using var app = result.app;
using var mainWindow = result.mainWindow;

app.Init();
app.Run(mainWindow);
return 0;
