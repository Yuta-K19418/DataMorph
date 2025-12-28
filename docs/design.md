# design_doc.md - DataMorph Architecture Design (C#/.NET 10)

## 1. Architecture Overview
DataMorph is designed as a high-performance pipeline. It avoids Garbage Collection (GC) pressure by using modern .NET memory management patterns.

## 2. Core Modules
### 2.1 Zero-Allocation Input Layer
- **Mmap Service**: Uses `MemoryMappedFile` for efficient random access to large files.
- **Pipelines Engine**: Implements `System.IO.Pipelines` to process data streams with minimal copying.
- **Safe Buffer Management**: Heavily utilizes `ReadOnlySpan<byte>` and `ReadOnlySequence<byte>` to parse data directly from memory buffers.

### 2.2 Low-Level Parsing (System.Text.Json)
- **Utf8JsonReader**: Uses the high-performance, forward-only JSON reader to scan JSON Arrays without allocating objects.
- **Schema Discovery**: A single-pass scanner that identifies unique keys in JSON records to build the dynamic table header.

### 2.3 Virtualized View Manager
- **Row Offset Indexer**: A background task that stores long-integers of row-start byte offsets to enable O(1) jumping to any record.
- **Lazy Transformer**: Transformation logic (Action Stack) is only applied to the rows visible in the current TUI viewport.

### 2.4 TUI Layer (Terminal.Gui v2.0)
- **Reactive UI**: Modern TUI components driven by a state-management system.
- **Async Responsiveness**: Separation of the UI thread from the data-processing thread to ensure the terminal remains responsive during massive file I/O.

## 3. Technical Strategies
- **Native AOT Optimization**: Avoiding Reflection and Dynamic Code Generation. Using Source Generators for JSON serialization and Dependency Injection.
- **SIMD via Intrinsics**: Using `System.Runtime.Intrinsics` and `Vector<T>` for hardware-accelerated searching of CSV delimiters and JSON tokens in a type-safe manner.
- **Action Stack**: An immutable list of transformation commands, serializable via `System.Text.Json.SourceGeneration`.

## 4. Data Flow
1. **Load**: `MmapService` maps the file.
2. **Index**: `BackgroundIndexer` scans for line breaks and records byte offsets.
3. **Render**: `ViewManager` requests a range of offsets, slices the memory with `Span<T>`, parses the records, and renders them to the `Terminal.Gui` Grid.
4. **Morph**: User actions update the `ActionStack`, triggering an instant re-render of the visible rows.
