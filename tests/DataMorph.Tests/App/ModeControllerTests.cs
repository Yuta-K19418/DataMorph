using AwesomeAssertions;
using DataMorph.App;
using DataMorph.Engine.IO.JsonLines;

namespace DataMorph.Tests.App;

public sealed class ModeControllerTests : IDisposable
{
    private readonly string _jsonlFilePath;

    public ModeControllerTests()
    {
        _jsonlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".jsonl");
    }

    public void Dispose()
    {
        if (File.Exists(_jsonlFilePath))
        {
            File.Delete(_jsonlFilePath);
        }
    }

    [Fact]
    public void Constructor_WithNullState_ThrowsArgumentNullException()
    {
        // Arrange
        // (no setup required)

        // Act
        var act = () => new ModeController(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithRowIndexerNull_DoesNothing()
    {
        // Arrange
        using var state = new AppState
        {
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = null
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithUnrelatedMode_DoesNothing()
    {
        // Arrange
        using var state = new AppState
        {
            CurrentMode = ViewMode.CsvTable
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.CsvTable);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_WithCachedSchema_ReusesSchema()
    {
        // Arrange
        var cachedScanner = new DataMorph.App.Schema.JsonLines.IncrementalSchemaScanner(_jsonlFilePath);
        var cachedSchema = new DataMorph.Engine.Models.TableSchema
        {
            Columns = [new DataMorph.Engine.Models.ColumnSchema { Name = "id", Type = DataMorph.Engine.Types.ColumnType.WholeNumber }],
            SourceFormat = DataMorph.Engine.Types.DataFormat.JsonLines
        };

        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath),
            JsonLinesSchemaScanner = cachedScanner,
            Schema = cachedSchema
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        // Should have reused the cached scanner and schema
        state.JsonLinesSchemaScanner.Should().BeSameAs(cachedScanner);
        state.Schema.Should().BeSameAs(cachedSchema);
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTreeMode_ScansSchemaAndSwitchesToTable()
    {
        // Arrange
        await File.WriteAllTextAsync(_jsonlFilePath, "{\"name\":\"Alice\"}\n{\"name\":\"Bob\"}");
        using var state = new AppState
        {
            CurrentFilePath = _jsonlFilePath,
            CurrentMode = ViewMode.JsonLinesTree,
            RowIndexer = new RowIndexer(_jsonlFilePath)
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTable);
        state.Schema.Should().NotBeNull();
        state.JsonLinesSchemaScanner.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleJsonLinesModeAsync_FromTableMode_RestoresTreeMode()
    {
        // Arrange
        using var state = new AppState
        {
            CurrentMode = ViewMode.JsonLinesTable
        };
        var controller = new ModeController(state);

        // Act
        var result = await controller.ToggleJsonLinesModeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        state.CurrentMode.Should().Be(ViewMode.JsonLinesTree);
    }
}
