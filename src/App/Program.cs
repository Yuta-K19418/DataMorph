using DataMorph.App;

var result = TuiApplication.Create();
using var app = result.app;
using var mainWindow = result.mainWindow;

app.Init();
app.Run(mainWindow);
