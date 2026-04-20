using AwesomeAssertions;
using DataMorph.App.Views;
using DataMorph.Engine.IO.Csv;
using DataMorph.Engine.Models;

namespace DataMorph.Tests.App.Views;

#pragma warning disable CA2000

public sealed class VirtualTableSourceTests : IDisposable
{
    private readonly string _testFilePath;

    public VirtualTableSourceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"virtualTableSource_{Guid.NewGuid()}.csv");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Dispose_DisposesCacheAndReader()
    {
        // Arrange
        var csvContent = "col1,col2\nval1,val2";
        File.WriteAllText(_testFilePath, csvContent);
        var indexer = new DataRowIndexer(_testFilePath);
        indexer.BuildIndex();
        var schema = new TableSchema
        {
            Columns =
            [
                new ColumnSchema { ColumnIndex = 0, Name = "col1", Type = DataMorph.Engine.Types.ColumnType.Text },
                new ColumnSchema { ColumnIndex = 1, Name = "col2", Type = DataMorph.Engine.Types.ColumnType.Text }
            ],
            SourceFormat = DataMorph.Engine.Types.DataFormat.Csv
        };
        var source = new VirtualTableSource(indexer, schema);
        _ = source[0, 0]; // Ensure cache and reader are initialized

        // Act
        source.Dispose();

        // Assert
        // Verify file is released
        using var stream = new FileStream(_testFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        stream.Should().NotBeNull();
    }
}
