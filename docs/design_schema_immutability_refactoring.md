# Schema Immutability Refactoring Design

## Background

This document describes the design decisions for refactoring `TableSchema` and `ColumnSchema` to follow proper immutability patterns in C#.

## Current Problems

### 1. Mutable Properties in Record Type

`ColumnSchema` is declared as a `record` but contains mutable properties:

```csharp
public sealed record ColumnSchema
{
    public required ColumnType Type { get; set; }  // Mutable!
    public bool IsNullable { get; set; }           // Mutable!
}
```

This violates the semantic contract of C# records, which are designed to be immutable value objects.

### 2. In-Place Mutation in RefineSchema

`CsvSchemaScanner.RefineSchema()` mutates the schema in-place:

```csharp
public static Result RefineSchema(TableSchema schema, CsvDataRow row)
{
    // ...
    columnSchema.MarkNullable();        // Mutates existing instance
    columnSchema.UpdateColumnType(...); // Mutates existing instance
}
```

This approach:
- Is not thread-safe (concurrent calls would cause race conditions)
- Violates record immutability semantics
- Makes it difficult to reason about state changes

### 3. RowCount Embedded in TableSchema

`TableSchema.RowCount` is a frequently-changing value embedded in a schema object:

```csharp
public sealed record TableSchema
{
    public long RowCount { get; init; }  // Changes frequently during loading
}
```

This couples stable structural information (columns) with volatile metadata (row count).

---

## Design Options Considered

| Option | Description | Thread-Safe | GC Pressure | Complexity |
|--------|-------------|-------------|-------------|------------|
| 1. Fully Immutable | Create new schema on every change | ✓ Excellent | △ High | ○ Simple |
| 2. Fully Mutable | Convert to class, in-place updates | ✗ Requires locks | ◎ Minimal | ○ Simple |
| 3. Builder Pattern | Mutable during build, immutable after | △ Partial | ○ Moderate | ✗ Complex |
| 4. Copy-on-Write | New instance only when values change | ✓ Excellent | ○ Moderate | ○ Simple |

### Option 1: Fully Immutable

Always create new instances regardless of whether values changed.

**Pros:**
- Simplest implementation
- Always thread-safe

**Cons:**
- Creates unnecessary allocations when no values change
- High GC pressure during initial schema stabilization

### Option 2: Fully Mutable (Class)

Convert `ColumnSchema` from `record` to `class` and use in-place mutation.

**Pros:**
- Minimal allocations
- Familiar mutation pattern

**Cons:**
- Loses record benefits (value equality, `GetHashCode`, `ToString`)
- Requires locking for thread safety
- More error-prone

### Option 3: Builder Pattern

Separate mutable `ColumnSchemaBuilder` from immutable `ColumnSchema`.

**Pros:**
- Clear separation of build phase vs. usage phase
- Immutable after construction

**Cons:**
- Doubles the number of types
- Requires managing both builder and final types
- Complex lifecycle management

### Option 4: Copy-on-Write (Selected)

Return new instance only when values actually change.

**Pros:**
- Maintains record semantics
- Thread-safe without locks
- Zero allocations when schema is stable
- Simple implementation with `with` expressions

**Cons:**
- Slightly more code in update methods

---

## Selected Approach: Copy-on-Write

### Rationale

1. **Record Semantics Compliance**: C# records should be immutable. Copy-on-Write honors this contract.

2. **Thread Safety**: Immutable objects are inherently thread-safe. No locks required for concurrent reads, and future multi-threaded access is safe by design.

3. **GC Efficiency**: After the schema stabilizes (types converge), allocations drop to zero. Most rows after the first ~200 won't trigger any changes.

4. **Code Consistency**: Other records in the codebase (e.g., `MorphAction`) follow immutable patterns. This maintains design consistency.

5. **Simplicity**: No additional builder classes. The `with` expression provides intuitive copy semantics.

---

## Design Details

### ColumnSchema Changes

Convert mutable `set` accessors to `init`:

```csharp
// Before
public sealed record ColumnSchema
{
    public required ColumnType Type { get; set; }
    public bool IsNullable { get; set; }
}

// After
public sealed record ColumnSchema
{
    public required ColumnType Type { get; init; }
    public bool IsNullable { get; init; }
}
```

### ColumnSchemaExtensions Changes

Return new `ColumnSchema` instead of mutating:

```csharp
// Before
public static void UpdateColumnType(this ColumnSchema schema, ColumnType observedType)
{
    schema.Type = ColumnTypeResolver.Resolve(schema.Type, observedType);
}

// After
public static ColumnSchema WithUpdatedType(this ColumnSchema schema, ColumnType observedType)
{
    var resolvedType = ColumnTypeResolver.Resolve(schema.Type, observedType);

    // Copy-on-Write: return same instance if no change
    if (schema.Type == resolvedType)
    {
        return schema;
    }

    return schema with { Type = resolvedType };
}
```

### CsvSchemaScanner.RefineSchema Changes

Return new `TableSchema` when columns change:

```csharp
// Before
public static Result RefineSchema(TableSchema schema, CsvDataRow row)

// After
public static Result<TableSchema> RefineSchema(TableSchema schema, CsvDataRow row)
```

The method will:
1. Process each column and collect updated schemas
2. Compare each updated schema with the original
3. Return the same `TableSchema` instance if nothing changed
4. Return a new `TableSchema` with updated columns only if changes occurred

---

## RowCount Separation

### Problem

`RowCount` changes frequently (potentially every row during loading) while column structure is stable. Including it in `TableSchema` would cause Copy-on-Write to allocate on every row.

### Solution

Remove `RowCount` from `TableSchema` and manage it separately:

```csharp
// Before
public sealed record TableSchema
{
    public required IReadOnlyList<ColumnSchema> Columns { get; init; }
    public long RowCount { get; init; }
    public required DataFormat SourceFormat { get; init; }
}

// After
public sealed record TableSchema
{
    public required IReadOnlyList<ColumnSchema> Columns { get; init; }
    public required DataFormat SourceFormat { get; init; }
    // RowCount managed by consumers (e.g., VirtualTableSource, CsvDataRowCache)
}
```

### Separation Criteria

Properties that belong in `TableSchema`:
- **Structural** information: `Columns`, `SourceFormat`
- Properties that change infrequently after initial scan

Properties to separate:
- **State/Metadata** that changes frequently: `RowCount`
- Runtime statistics

---

## Application to JsonLines and JsonArray

The Copy-on-Write pattern applies uniformly across all formats:

| Format | Schema Update Scenarios | Copy-on-Write Applied |
|--------|------------------------|----------------------|
| CSV | Type inference during scroll, nullable detection | ✓ |
| JsonLines | New properties discovered, type changes | ✓ |
| JsonArray | Same as JsonLines | ✓ |

### Common Pattern

1. **Initial schema creation**: Infer from first batch of data
2. **RefineSchema calls**: Update types/nullable as new data arrives (only allocate on change)
3. **Stable phase**: Zero allocations as schema converges

---

## Schema Inference Optimization: Initial 200-Row Scan

### Current Behavior

```
Row 1:      ScanSchema → Initial schema
Row 2~N:    RefineSchema × (N-1) times
```

The schema starts from a single row and may change frequently as more rows reveal different types.

### Improved Behavior

```
Row 1~200:  ScanSchema → Schema inferred from 200 rows (stable)
Row 201~N:  RefineSchema (mostly no-change, zero allocations)
```

### Benefits

1. **Faster stabilization**: 200 rows provide better type inference than 1 row
2. **Fewer allocations**: Schema is already stable by row 201
3. **Better type accuracy**: Edge cases (nulls, different number formats) are captured early

### Configuration

- Default initial scan rows: 200
- Configurable via parameter in schema scanner
- Can be reduced for very wide tables (memory consideration)

---

## GC Pressure Estimation (10,000-Row Scroll)

### Before (Current Implementation)

```
Row 1:        1 TableSchema + N ColumnSchema allocations
Row 2~10000:  Potential mutations on every row (not tracked as allocations,
              but violates immutability contract)
```

### After (Copy-on-Write with 200-Row Initial Scan)

```
Row 1~200:    Initial scan phase
              → 1 TableSchema + N ColumnSchema allocations

Row 201~10000: Stable phase
              → ~0 allocations (same instances returned)
```

### Conclusion

Copy-on-Write combined with initial 200-row scanning results in:
- Minimal GC pressure after initial stabilization
- Thread-safe operations without locks
- Clean immutability semantics

---

## Files to Modify (Implementation Reference)

| File | Changes |
|------|---------|
| `src/Engine/Models/ColumnSchema.cs` | `set` → `init` for `Type` and `IsNullable` |
| `src/Engine/Models/TableSchema.cs` | Remove `RowCount` property |
| `src/Engine/IO/ColumnSchemaExtensions.cs` | Return `ColumnSchema` instead of `void` |
| `src/Engine/IO/CsvSchemaScanner.cs` | Return `Result<TableSchema>` from `RefineSchema`, add 200-row initial scan |
| `src/App/Views/VirtualTableSource.cs` | Manage `RowCount` locally (already uses `_cache.TotalRows`) |
| Test files | Update to match new method signatures |

---

## Summary

This refactoring:

1. **Fixes** the record immutability violation in `ColumnSchema`
2. **Enables** thread-safe schema operations via Copy-on-Write
3. **Optimizes** GC pressure by avoiding unnecessary allocations
4. **Separates** volatile metadata (`RowCount`) from stable structure
5. **Improves** schema stability through initial multi-row scanning
