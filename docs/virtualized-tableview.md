# Implementation Plan: Virtualized TableView for Lag-Free Scrolling

## Overview

Implement a virtualized `TableView` to enable lag-free scrolling through millions of CSV/JSON records. This is achieved by creating a custom `ITableSource` implementation (`VirtualTableSource`) that integrates with the Engine layer's `CsvRowIndexer` and `CsvRowCache`.

## Background

- **Current State**: `PlaceholderView` displays only the file path after selection.
- **Existing Infrastructure**:
  - Engine layer: `MmapService`, `CsvRowIndexer`, `CsvRowCache`, `TableSchema`.
  - App layer: `MainWindow`, `AppState`, view switching mechanism.
- **Goal**: Use `Terminal.Gui`'s built-in `TableView` to create a virtualized grid that renders only visible rows, ensuring high performance with large datasets.

## Objectives

1.  Implement `VirtualTableSource` which acts as a bridge between the data engine and the UI.
2.  Integrate with `CsvRowIndexer` and `CsvRowCache` for efficient, on-demand row access.
3.  Utilize the built-in features of `Terminal.Gui.TableView` for rendering, scrolling, and keyboard navigation.
4.  Display column headers derived from `TableSchema`.
5.  Ensure minimal memory usage by not loading the entire file into memory.

## Implementation Details

### 1. Architecture

**Component Interaction:**

The `TableView` in the UI layer requests only the visible data from `VirtualTableSource`. `VirtualTableSource` in turn retrieves this data from `CsvRowCache`, which intelligently fetches and caches row data from the underlying `CsvRowIndexer` and memory-mapped file.

**Data Flow:**
```
CSV File
   ├─── Used by CsvRowIndexer (creates row offsets)
   └─── Used by CsvRowReader (reads actual row data)
           ↓
      CsvRowCache (manages cached rows using CsvRowIndexer and CsvRowReader)
           ↓
      VirtualTableSource (implements ITableSource)
           ↓
      TableView (Terminal.Gui component for display)
```

### 2. Core Components

#### VirtualTableSource.cs
This class is the core of the virtualized solution. It implements the `Terminal.Gui.Views.ITableSource` interface, which `TableView` uses to request data for display.

```csharp
namespace DataMorph.App.Views;

internal sealed class VirtualTableSource : ITableSource
{
    private readonly CsvRowCache _cache;
    private readonly TableSchema _schema;
    private readonly string[] _columnNames;

    public VirtualTableSource(CsvRowIndexer indexer, TableSchema schema)
    {
        _schema = schema;
        _columnNames = _schema.Columns.Select(c => c.Name).ToArray();
        _cache = new CsvRowCache(indexer, _schema.ColumnCount);
    }

    public int Rows => _cache.TotalRows;
    public int Columns => _schema.ColumnCount;
    public string[] ColumnNames => _columnNames;

    public object this[int row, int col]
    {
        get
        {
            var rowData = _cache.GetRow(row);
            // ... logic to return cell data ...
        }
    }
}
```

**Key Features:**
- **On-Demand Data Access**: The `this[row, col]` indexer is called by `TableView` only for the rows and columns it needs to draw.
- **Data Caching**: Delegates row retrieval to `CsvRowCache`, which minimizes redundant parsing and I/O operations.
- **Decoupling**: Separates the UI (`TableView`) from the data source logic, adhering to a clean architecture.

#### ViewportState.cs
While `TableView` manages its own viewport, this class can be used to track application-level state related to the data view, such as the currently selected cell or filtering state in future extensions.

```csharp
namespace DataMorph.App.Views;

internal sealed class ViewportState
{
    public int TopRow { get; set; }
    public int SelectedRow { get; set; }
    public int SelectedColumn { get; set; }
    // ... other state properties
}
```

### 3. Integration with Engine Layer

#### Update MainWindow.cs
When a file is selected, `MainWindow` is responsible for initializing the engine components and setting up the `TableView` with the `VirtualTableSource`.

```csharp
private void ShowDataView(string filePath)
{
    // 1. Initialize CsvRowIndexer and CsvSchemaCreator
    var indexer = new CsvRowIndexer(filePath);
    indexer.BuildIndex(); // Build the index synchronously

    var schemaResult = CsvSchemaCreator.CreateSchemaFromCsvHeader(filePath, () => indexer.TotalRows);
    // ... error handling ...

    _state.RowIndexer = indexer;
    _state.Schema = schemaResult.Value;

    // 2. Create the TableView and its source
    var tableSource = new VirtualTableSource(_state.RowIndexer, _state.Schema);
    var tableView = new TableView
    {
        X = 0, Y = 0,
        Width = Dim.Fill(),
        Height = Dim.Fill(),
        Table = tableSource
    };

    // 3. Switch the current view
    SwitchToView(tableView);
}
```

#### Update AppState.cs
The `AppState` class holds the state of the engine components, making them accessible throughout the application.

```csharp
internal sealed class AppState
{
    public string CurrentFilePath { get; set; } = string.Empty;
    public ViewMode CurrentMode { get; set; } = ViewMode.FileSelection;

    // Engine component references
    public CsvRowIndexer? RowIndexer { get; set; }
    public TableSchema? Schema { get; set; }
}
```

### 4. Keyboard Shortcuts

All standard keyboard navigation is handled automatically by the `Terminal.Gui.TableView` component:
- **Arrow Keys**: Navigate rows and columns.
- **Page Up/Down**: Jump by viewport height.
- **Home/End**: Jump to the first/last row or column.

Application-specific shortcuts remain:
- **Ctrl+X**: Quit.
- **Ctrl+O**: Open file.

### 5. Performance Considerations

- **Virtualization**: The `TableView` only requests data for visible cells, so memory usage is independent of file size.
- **Efficient Caching**: `CsvRowCache` reduces the cost of accessing frequently viewed rows.
- **Zero-Allocation Parsing**: The underlying `CsvRowReader` uses `ReadOnlySpan<byte>` to avoid string allocations during parsing, minimizing GC pressure.

### 6. Testing Strategy

#### Unit Tests

1.  **`tests/DataMorph.Tests/App/Views/VirtualTableSourceTests.cs`**:
    - Test constructor and property initialization.
    - Test `this[row, col]` indexer with valid and out-of-range inputs.
    - Mock `CsvRowCache` to verify that `VirtualTableSource` correctly retrieves and returns data.

2.  **`tests/DataMorph.Tests/Engine/IO/CsvRowCacheTests.cs`**:
    - Test caching logic: ensure rows are fetched on the first request and retrieved from cache on subsequent requests.
    - Test cache eviction policies if any are implemented.

#### Manual Testing Checklist

1.  Open a small CSV file (< 100 rows): Verify header and data render correctly.
2.  Open a large CSV file (> 1 million rows):
    - Verify the file opens quickly.
    - Verify smooth, lag-free scrolling using arrow keys and Page Up/Down.
    - Monitor memory usage to confirm it remains low and constant during scrolling.
3.  Test edge cases:
    - Empty CSV file.
    - CSV with only a header.
    - CSV with ragged rows (rows with a different number of columns).

### 7. Deferred Features

The following features are **explicitly out of scope** for this issue:
-   [ ] Column morphing (Delete, Rename, Cast).
-   [ ] Filtering/search.
-   [ ] Horizontal scrolling for wide tables (though `TableView` has some built-in support).
-   [ ] Column resizing by the user.
-   [ ] Cell editing.
-   [ ] Export functionality.
This implementation focuses solely on high-performance, read-only virtualized rendering.
