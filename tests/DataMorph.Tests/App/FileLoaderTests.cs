using DataMorph.App;
using Xunit;

namespace DataMorph.Tests.App;

public sealed class FileLoaderTests : IDisposable
{
    private readonly string _csvFilePath;
    private readonly string _jsonlFilePath;
    private readonly string _unsupportedFilePath;
    private readonly string _emptyCsvFilePath;
    private readonly string _nonExistentFilePath;
    private readonly string _headerOnlyCsvFilePath;

    public FileLoaderTests()
    {
        _csvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _jsonlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
        _unsupportedFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".json");
        _emptyCsvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
        _nonExistentFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.csv");
        _headerOnlyCsvFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".csv");
    }

    public void Dispose()
    {
        foreach (var path in new[] { _csvFilePath, _jsonlFilePath, _unsupportedFilePath, _emptyCsvFilePath, _headerOnlyCsvFilePath })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WithCsvFile_UpdatesStateAndMode()
    {
        // Arrange
        await File.WriteAllTextAsync(_csvFilePath, "Name,Age\nAlice,30\nBob,25");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_csvFilePath);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ViewMode.CsvTable, state.CurrentMode);
        Assert.NotNull(state.Schema);
        Assert.NotNull(state.CsvIndexer);
    }

    [Fact]
    public async Task LoadAsync_WithJsonLinesFile_UpdatesStateAndMode()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}\n{\"name\":\"Bob\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_jsonlFilePath);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ViewMode.JsonLinesTree, state.CurrentMode);
        Assert.NotNull(state.JsonLinesIndexer);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTreeMode_ScansSchemaAndSwitchesToTable()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}\n{\"name\":\"Bob\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);
        await loader.LoadAsync(_jsonlFilePath);

        // Act
        var result = await loader.ToggleJsonLinesModeAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ViewMode.JsonLinesTable, state.CurrentMode);
        Assert.NotNull(state.Schema);
        Assert.NotNull(state.JsonLinesSchemaScanner);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTableMode_RestoresTreeMode()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);
        await loader.LoadAsync(_jsonlFilePath);
        await loader.ToggleJsonLinesModeAsync();

        // Act
        var result = await loader.ToggleJsonLinesModeAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ViewMode.JsonLinesTree, state.CurrentMode);
    }

    [Fact]
    public async Task LoadAsync_WithUnsupportedFormat_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_unsupportedFilePath, "{\"key\":\"value\"}");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_unsupportedFilePath);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains(".json", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(state.CsvIndexer);
        Assert.Null(state.JsonLinesIndexer);
    }

    [Fact]
    public async Task LoadAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_nonExistentFilePath);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("does not exist", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(state.CsvIndexer);
        Assert.Null(state.JsonLinesIndexer);
    }

    [Fact]
    public async Task LoadAsync_WithEmptyFile_ReturnsFailure()
    {
        // Arrange
        await File.WriteAllBytesAsync(_emptyCsvFilePath, []);
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_emptyCsvFilePath);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(state.CsvIndexer);
        Assert.Null(state.JsonLinesIndexer);
    }

    [Fact]
    public async Task LoadAsync_WithCsvContainingHeaderOnly_LoadsSuccessfully()
    {
        // Arrange
        // A CSV with a header row but no data rows is valid; schema is inferred from column names.
        await File.WriteAllTextAsync(_headerOnlyCsvFilePath, "Name,Age\n");
        var state = new AppState();
        using var loader = new FileLoader(state);

        // Act
        var result = await loader.LoadAsync(_headerOnlyCsvFilePath);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ViewMode.CsvTable, state.CurrentMode);
        Assert.NotNull(state.Schema);
        Assert.NotNull(state.Schema.Columns);
        Assert.Equal(2, state.Schema.Columns.Count);
        Assert.NotNull(state.CsvIndexer);
    }

    [Fact]
    public async Task LoadAsync_JsonLines_WhenCalledTwice_CancelsPreviousBuildIndex()
    {
        // Arrange
        var lines = Enumerable.Range(0, 800).Select(i => $"{{\"id\":{i}}}").ToList();
        await File.WriteAllLinesAsync(_jsonlFilePath, lines);
        var state = new AppState();
        using var loader = new FileLoader(state);
        var firstIndexerCompleted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await loader.LoadAsync(_jsonlFilePath);
        Assert.NotNull(state.JsonLinesIndexer);
        var completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        state.JsonLinesIndexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        // Act
        var secondLines = Enumerable.Range(0, 100).Select(i => $"{{\"id\":{i}}}").ToList();
        await File.WriteAllLinesAsync(_jsonlFilePath, secondLines);
        var result = await loader.LoadAsync(_jsonlFilePath);

        // Assert
        Assert.True(result.IsSuccess);

        await Task.WhenAny(
            completed.Task,
            Task.Delay(TimeSpan.FromSeconds(5))
        ).ConfigureAwait(true);
        Assert.True(completed.Task.Result);
        Assert.NotNull(state.JsonLinesIndexer);
    }

    [Fact]
    public async Task LoadAsync_Csv_WhenCalledTwice_CancelsPreviousBuildIndex()
    {
        // Arrange
        var header = "id,name";
        var rows = Enumerable.Range(0, 800).Select(i => $"{i},Row{i}");
        var content = string.Join("\n", [header, .. rows]);
        await File.WriteAllTextAsync(_csvFilePath, content);
        var state = new AppState();
        using var loader = new FileLoader(state);
        var completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        state.CsvIndexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        // Act
        var secondRows = Enumerable.Range(0, 100).Select(i => $"{i},Row{i}");
        var secondContent = string.Join("\n", [header, .. secondRows]);
        await File.WriteAllTextAsync(_csvFilePath, secondContent);
        var result = await loader.LoadAsync(_csvFilePath);

        // Assert
        Assert.True(result.IsSuccess);

        await Task.WhenAny(
            completed.Task,
            Task.Delay(TimeSpan.FromSeconds(5))
        ).ConfigureAwait(true);
        Assert.True(completed.Task.Result);
        Assert.NotNull(state.CsvIndexer);
    }

    [Fact]
    public async Task Dispose_CancelsBuildIndex()
    {
        // Arrange
        var lines = Enumerable.Range(0, 1200).Select(i => $"{{\"id\":{i}}}").ToList();
        await File.WriteAllLinesAsync(_jsonlFilePath, lines);
        var state = new AppState();
        var loader = new FileLoader(state);
        var completed = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        state.JsonLinesIndexer.BuildIndexCompleted += () => completed.TrySetResult(true);

        // Act
        loader.Dispose();

        // Assert
        await Task.WhenAny(
            completed.Task,
            Task.Delay(TimeSpan.FromSeconds(5))
        ).ConfigureAwait(true);
        Assert.True(completed.Task.Result);
    }
}
