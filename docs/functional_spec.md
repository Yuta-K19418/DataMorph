# Functional Specification

## 1. Project Overview
DataMorph is a high-performance TUI/CLI tool built with C# (.NET 10, Native AOT) for browsing and transforming massive JSON and CSV files. It utilizes an "Explorer-First" paradigm, allowing users to navigate hierarchical data and "morph" specific branches into structured tables.

## 2. Format Identification & Initial State
| Format | Header/Indicator | Initial Mode | Indexing Target |
| :--- | :--- | :--- | :--- |
| **JSON Object** | Starts with `{` | Explorer (Tree) | Top-level keys only |
| **JSON Array** | Starts with `[` | Explorer (Tree) | Element boundaries `{` |
| **JSON Lines** | One `{}` per line | Table (Grid) | Line breaks `\n` |
| **CSV** | Delimiter detected | Table (Grid) | Line breaks `\n` |

## 3. Priority 1: Instant Viewer (Foundation)
**Goal**: Sub-second startup for files 50GB+ with constant memory footprint.
- **Byte-Offset Indexer**: Background task recording `(long Offset, int Length)` for each record.
- **Virtual Viewport**: Renders only visible rows (approx. 20-50). Data is parsed via `Utf8JsonReader` on `ReadOnlySpan<byte>` only when needed.

## 4. Priority 2: Selective Morphing (Tree to Table)
**Goal**: Seamless transition from hierarchical navigation to tabular processing.
- **Explorer Mode**: Lazy-load node children. Use `ENTER` to expand and `M` to Morph the selected array/object into a table.
- **Simplification Rule**: To maintain fixed row height and performance:
    - Primitives (String/Num/Bool): Displayed as-is.
    - Objects/Arrays: Displayed as `{ Object }` or `[ n items ]`.
    - Logic: Use `Utf8JsonReader.Skip()` for deep structures unless manually expanded.

## 5. Priority 3: Transformation Actions (Morphism)
### 5.1 CSV Specific
- **Encoding (E)**: Toggle UTF-8 / Shift-JIS with real-time UI refresh.
- **Cast Type (C)**: Explicitly define columns as Int, Decimal, DateTime, or Bool.
### 5.2 JSON Specific
- **Flatten (F)**: Expand `{ Object }` into dot-notation columns (e.g., `user.id`).
- **Explode (X)**: Pivot `[ Array ]` into multiple rows, duplicating parent row data.
### 5.3 Common
- **Drop (D)**: Logically remove column from view and export.
- **Rename (R)**: Assign alias for output headers.

## 6. Priority 4: Dynamic Union & Array Exploding
**Goal**: Manage inconsistent schemas and 1:N data relationships.
- **Dynamic Union**: Discover new columns during scrolling. Missing keys are rendered as `<null>`.
- **Explode Logic**: A single file-record maps to multiple virtual TUI rows. Parent fields are repeated across child rows.



## 7. Priority 5: Recipe & Automation
**Goal**: Persist TUI actions into a YAML recipe for headless CLI batch processing.
```yaml
source:
  path: "large_data.json"
  format: "json_array"
  root_path: "$.data.orders[*]"
actions:
  - { action: "drop", column: "secret_id" }
  - { action: "cast", column: "price", to: "decimal" }
  - { action: "explode", column: "items" }
output:
  format: "csv"
  path: "clean_data.csv"
```

## 8. Non-Functional Requirements
- Runtime: .NET 10 Native AOT (Zero dependencies).
- Safety: Strictly managed code (No unsafe).
- Memory: Cap under 100MB RAM even for Multi-GB files.
