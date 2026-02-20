# Design: LazyTransformer with Immutable Action Stack

## Overview

`LazyTransformer` is an Engine-layer component that wraps an `ITableSource` and applies an
ordered list of `MorphAction`s (the Action Stack) lazily — only to the rows and cells currently
requested by the `TableView`. This ensures that even millions of rows can be transformed without
any up-front processing cost.

## Architecture

```
VirtualTableSource / JsonLinesTableSource   ← raw data source
              ↓
       LazyTransformer                      ← ITableSource wrapper (new)
              ↓
           TableView                        ← Terminal.Gui display
```

`LazyTransformer` implements `ITableSource`, so it is a transparent drop-in between the
existing data source and the `TableView`.

## Responsibilities

1. **Schema transformation**: On construction, apply the full Action Stack sequentially to
   derive the output `TableSchema` (reflecting renames, deletes, and type casts).
2. **Column mapping**: Build an `int[]` array mapping each output column index to its
   corresponding source column index, accounting for deletions.
3. **Lazy cell transformation**: When `this[row, col]` is called, map the output column index
   back to the source column index and delegate to the underlying source. Apply formatting for
   columns targeted by a `CastColumnAction`.

## New File

**`src/Engine/LazyTransformer.cs`**

```
namespace DataMorph.Engine;

internal sealed class LazyTransformer : ITableSource
{
    LazyTransformer(
        ITableSource source,
        TableSchema originalSchema,
        IReadOnlyList<MorphAction> actions)
}
```

### Properties

| Property | Implementation |
|---|---|
| `Rows` | Delegates to `source.Rows` |
| `Columns` | `_transformedSchema.ColumnCount` |
| `ColumnNames` | Derived from `_transformedSchema.Columns` |
| `this[row, col]` | `source[row, _sourceColumnIndices[col]]` + optional cast formatting |

## Action Stack Processing

On construction, a mutable working list is maintained to simulate applying actions in order:

```
WorkingColumn
{
    SourceIndex : int         // column index in the raw source (never changes)
    Name        : string      // current name (updated by RenameColumnAction)
    Type        : ColumnType  // current type (updated by CastColumnAction)
    IsNullable  : bool
}
```

Each action is processed sequentially:

| Action | Behaviour |
|---|---|
| `RenameColumnAction` | Find `WorkingColumn` by current `Name`, update `Name` to `NewName` |
| `DeleteColumnAction` | Find `WorkingColumn` by current `Name`, remove from working list |
| `CastColumnAction` | Find `WorkingColumn` by current `Name`, update `Type` to `TargetType` |

After processing all actions, build `_transformedSchema` and `_sourceColumnIndices` from the
remaining working list.

### Example

Source schema: `[A(Text), B(Text), C(Text)]`

Actions:
1. `RenameColumnAction { OldName="A", NewName="X" }`
2. `DeleteColumnAction { ColumnName="B" }`
3. `CastColumnAction   { ColumnName="C", TargetType=WholeNumber }`

Working list after processing:

| Output Index | SourceIndex | Name | Type |
|---|---|---|---|
| 0 | 0 | X | Text |
| 1 | 2 | C | WholeNumber |

Result:
- `_transformedSchema.Columns` = `[X(Text), C(WholeNumber)]`
- `_sourceColumnIndices` = `[0, 2]`

## Cast Formatting

When `this[row, col]` is called and the column has been cast, the raw string value from the
source is formatted before returning. All return values are `string` for display consistency.

| Target Type | Formatting |
|---|---|
| `Text` | Return raw value as-is |
| `WholeNumber` | `long.TryParse` → formatted string; `"<invalid>"` on failure |
| `FloatingPoint` | `double.TryParse` → formatted string; `"<invalid>"` on failure |
| `Boolean` | `bool.TryParse` → `"true"` / `"false"`; `"<invalid>"` on failure |
| `Timestamp` | `DateTime.TryParse` → formatted string; `"<invalid>"` on failure |
| `JsonObject` / `JsonArray` | Return raw value as-is (no re-parsing) |

## Immutability

The Action Stack (`IReadOnlyList<MorphAction>`) is immutable. Applying a new action means
constructing a new `LazyTransformer` with the extended list.

Construction cost is O(A × C) where A = number of actions and C = number of columns — negligible
for realistic workloads.

## Error Handling

Actions targeting a column name that does not exist in the current working list are silently
skipped. This handles:
- A column already deleted before a subsequent rename or cast targets it.
- Schema drift between action recording and playback.

Out-of-range `row` or `col` access in `this[row, col]` throws `ArgumentOutOfRangeException`,
consistent with the existing `VirtualTableSource` and `JsonLinesTableSource` behaviour.

## Integration with Existing Code

`MainWindow` will wrap the existing `ITableSource` with `LazyTransformer` before assigning it
to `TableView.Table`. When the Action Stack is empty `LazyTransformer` is a pure passthrough.

```
// Before (CSV)
Table = new VirtualTableSource(indexer, schema)

// After (CSV)
Table = new LazyTransformer(new VirtualTableSource(indexer, schema), schema, actionStack)
```

The same pattern applies to `JsonLinesTableSource`.

`AppState` will hold the current action stack:

```csharp
public IReadOnlyList<MorphAction> ActionStack { get; set; } = [];
```

## Testing

New file: `tests/DataMorph.Tests/Engine/LazyTransformerTests.cs`

### Schema transformation
- Rename: output schema reflects new name; `SourceIndex` of the column is preserved.
- Delete: deleted column absent from output schema; remaining columns re-indexed correctly.
- Cast: output schema reflects new `ColumnType`.
- Ordered actions: rename-then-delete operates on the renamed name.

### Column mapping
- `_sourceColumnIndices` maps correctly after one or more deletions.
- All columns deleted: `Columns == 0`, no cell access possible.

### Cell value passthrough and cast formatting
- Empty action stack: `this[row, col]` returns the same value as the underlying source.
- Cast to `WholeNumber`: valid integer string formatted correctly; invalid input returns `"<invalid>"`.
- Cast to `FloatingPoint`, `Boolean`, `Timestamp`: same pattern.

### Error handling
- Action targeting a non-existent column name: silently skipped, no exception.
- `this[row, col]` with out-of-range `row` or `col`: throws `ArgumentOutOfRangeException`.
