# spec.md - DataMorph Project Specification (C#/.NET 10 Edition)

## 1. Project Vision
**"High-Performance Data Morphing for Everyone."**
DataMorph is a TUI-driven data transformation tool built with .NET 10. It empowers developers to explore, clean, and automate processing for massive **JSON Arrays** and **CSV** files without the steep learning curve of low-level languages, while maintaining native-level performance.

## 2. Core Values
- **Maintainable Performance**: Leveraging .NET 10 Native AOT for lightning-fast startup and memory efficiency without sacrificing code readability.
- **Visual-to-Automated Workflow**: Bridge the gap between interactive TUI exploration and high-throughput CLI batch processing.
- **Managed Safety**: Achieving zero-allocation data processing using modern C# features (`Span<T>`, `Memory<T>`) without relying on `unsafe` code.

## 3. Key Features
### 3.1 Intelligent Flattening
- Automatically expand nested JSON structures into a flat table view using dot-notation (e.g., `order.details.price`).
- Interactive schema inference to handle inconsistent JSON records gracefully.

### 3.2 TUI Explorer (Terminal.Gui v2.0)
- **Virtual Grid**: Lag-free scrolling through millions of records using a virtualized data source.
- **Key-Driven Morphing**: Delete columns (`D`), Rename (`R`), and Cast types (`C`) with instant visual feedback.
- **Fast Filtering**: Real-time record reduction using a high-speed string search engine.

### 3.3 Recipe-Based Automation
- **Recipe Export**: Save TUI operations as a `morph-recipe.yaml`.
- **CLI Headless Mode**: Apply saved recipes to massive files at physical disk limits.

## 4. Performance Targets
- **Near-Zero RAM Growth**: Constant memory usage regardless of file size through `System.IO.Pipelines` and `Span<T>`.
- **Native AOT**: Single-file executable with no external dependencies and millisecond-level startup.
