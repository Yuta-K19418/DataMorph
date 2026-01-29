# CSV Schema Scanner - Design Document

## Scope
This implementation focuses on **header parsing and type inference** for CSV files. The schema scanner analyzes a single row to infer column types with zero allocations in hot paths.

---

## 1. Requirements

### Functional Requirements
- Parse header row to extract column names
- Infer column types by analyzing a single data row
- Support the following type detection:
  - `WholeNumber` (int64): Value parses as integer
  - `FloatingPoint` (double): Value parses as decimal
  - `Timestamp` (DateTime): Value parses as date/time
  - `Boolean`: Value is `true`/`false` (case-insensitive)
  - `Text`: Fallback when other types fail (including empty values)
- Track nullable status (whether column contains empty value)

### Non-Functional Requirements
- **Zero allocations** during type inference (hot path)
- **Native AOT compatible** (no reflection)
- Use `ReadOnlySpan<char>` for parsing
- Efficient for large files (only reads single row for inference)

---

## 2. Design

### 2.1 Class: `CsvTypeInferrer`

#### Responsibilities
- Analyze char spans to infer column types
- Determine column type based on parsing result

#### Key Design Decisions

**Type Priority Order:**
When a value can be parsed as multiple types (e.g., "123" is valid as both int and double), use the most specific type:
1. `Boolean` (most specific: only "true" or "false")
2. `WholeNumber` (integers only)
3. `FloatingPoint` (any numeric value)
4. `Timestamp` (date/time formats)
5. `Text` (fallback)

**Type Inference Strategy:**
- For each column value, try parsing in priority order
- Return the most specific type that successfully parses

**Boolean Detection:**
- Case-insensitive comparison: `true`, `TRUE`, `True`, `false`, `FALSE`, `False`

**Numeric Parsing:**
- Integer detection: `long.TryParse`
- Decimal detection: `double.TryParse`

**DateTime Parsing:**
- Use `DateTime.TryParse`
- Support ISO 8601 format

### 2.2 Class: `CsvSchemaScanner`

#### Responsibilities
- Coordinate type inference for each column
- Build final `TableSchema` with inferred types

#### Key Design Decisions

**Single Row Strategy:**
- Use a single data row for type inference
- Caller provides column names and the row to analyze
- Simple and efficient approach

### 2.3 API Design

```csharp
/// <summary>
/// Infers column type from char data.
/// </summary>
public static class CsvTypeInferrer
{
    /// <summary>
    /// Infers the most specific type for a char span.
    /// </summary>
    /// <param name="value">The char span to analyze.</param>
    /// <returns>The inferred ColumnType.</returns>
    public static ColumnType InferType(ReadOnlySpan<char> value);

    /// <summary>
    /// Tries to parse value as Boolean.
    /// </summary>
    public static bool TryParseBoolean(ReadOnlySpan<char> value, out bool result);

    /// <summary>
    /// Tries to parse value as WholeNumber (int64).
    /// </summary>
    public static bool TryParseWholeNumber(ReadOnlySpan<char> value, out long result);

    /// <summary>
    /// Tries to parse value as FloatingPoint (double).
    /// </summary>
    public static bool TryParseFloatingPoint(ReadOnlySpan<char> value, out double result);

    /// <summary>
    /// Tries to parse value as Timestamp (DateTime).
    /// </summary>
    public static bool TryParseTimestamp(ReadOnlySpan<char> value, out DateTime result);
}

/// <summary>
/// Scans CSV data to infer schema (header + column types).
/// </summary>
public static class CsvSchemaScanner
{
    /// <summary>
    /// Scans a single CSV row and returns the inferred schema.
    /// </summary>
    /// <param name="columnNames">Column names from header.</param>
    /// <param name="row">The single row to analyze for type inference.</param>
    /// <param name="totalRowCount">Total number of rows in the CSV.</param>
    /// <returns>Result containing TableSchema or error message.</returns>
    public static Result<TableSchema> ScanSchema(
        IReadOnlyList<string> columnNames,
        CsvDataRow row,
        long totalRowCount);
}
```

### 2.4 Type Inference Algorithm

```
Input: Single char span value
Output: ColumnType

1. Try parse in priority order (most specific first):
   - If TryParseBoolean succeeds: Return ColumnType.Boolean
   - If TryParseWholeNumber succeeds: Return ColumnType.WholeNumber
   - If TryParseFloatingPoint succeeds: Return ColumnType.FloatingPoint
   - If TryParseTimestamp succeeds: Return ColumnType.Timestamp

2. Fallback: Return ColumnType.Text (including empty values)

Note: If value is empty, mark column as nullable but still return Text type.
```

### 2.5 Integration Flow

```
┌─────────────────────────────────────────────────────────────┐
│                     CsvSchemaScanner                        │
│                                                             │
│  1. Receive column names and single data row                │
│                                                             │
│  2. For each column value:                                  │
│     └─> CsvTypeInferrer.InferType()                         │
│     └─> Determine IsNullable (if value is empty)            │
│                                                             │
│  3. Build TableSchema                                       │
│     └─> ColumnSchema[] with names, types, nullable flags    │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Implementation Plan

### 3.1 CsvTypeInferrer Implementation

**Boolean Parsing:**
```csharp
public static bool TryParseBoolean(ReadOnlySpan<char> value, out bool result)
{
    result = false;
    var trimmed = value.Trim();

    if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
    {
        result = true;
        return true;
    }

    if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
    {
        result = false;
        return true;
    }

    return false;
}
```

**Numeric Parsing:**
```csharp
public static bool TryParseWholeNumber(ReadOnlySpan<char> value, out long result)
{
    result = 0;
    var trimmed = value.Trim();
    if (trimmed.IsEmpty) return false;

    return long.TryParse(trimmed, out result);
}

public static bool TryParseFloatingPoint(ReadOnlySpan<char> value, out double result)
{
    result = 0;
    var trimmed = value.Trim();
    if (trimmed.IsEmpty) return false;

    return double.TryParse(trimmed, out result);
}
```

### 3.2 Edge Cases

| Case | Handling |
|------|----------|
| Empty file | Return error "CSV file is empty" |
| Header only (no data rows) | All columns are `Text` type, nullable |
| Empty column header | Generate name `Column{index+1}` |
| Empty value | `Text` type, mark as nullable |
| Quoted field with newline | Use RFC 4180 compliant parsing |
| Numeric with leading zeros | `Text` (e.g., "007" stays as text) |
| Scientific notation | `FloatingPoint` (e.g., "1.5e10") |

### 3.3 Performance Considerations

**Zero-Allocation Strategy:**
- Use `ReadOnlySpan<char>` throughout
- Avoid string allocations during type inference
- Only allocate when building final `TableSchema`

**Parsing Optimization:**
- Short-circuit on first successful parse (priority order)

---

## 4. Files to Modify/Create

| File | Action | Purpose |
|------|--------|---------|
| `src/Engine/IO/CsvTypeInferrer.cs` | Create | Type inference |
| `src/Engine/IO/CsvSchemaScanner.cs` | Create | Schema scanning coordination |
| `tests/DataMorph.Tests/IO/CsvTypeInferrerTests.cs` | Create | Type inference unit tests |
| `tests/DataMorph.Tests/IO/CsvSchemaScannerTests.cs` | Create | Schema scanner integration tests |

---

## 5. Testing Strategy

### 5.1 Unit Tests for CsvTypeInferrer

**Boolean Parsing:**
- `"true"` / `"false"` (lowercase)
- `"TRUE"` / `"FALSE"` (uppercase)
- `"True"` / `"False"` (mixed case)
- `"  true  "` (with whitespace)
- `"truee"` / `"tru"` (invalid)

**WholeNumber Parsing:**
- `"123"` (positive)
- `"-456"` (negative)
- `"0"` (zero)
- `"  789  "` (with whitespace)
- `"12.34"` (decimal - should fail)
- `"12abc"` (trailing chars - should fail)
- `"9223372036854775807"` (max int64)

**FloatingPoint Parsing:**
- `"12.34"` (decimal)
- `"-0.5"` (negative decimal)
- `"1.5e10"` (scientific notation)
- `"123"` (integer - should succeed as double)
- `"NaN"` / `"Infinity"` (special values)

**Timestamp Parsing:**
- `"2024-01-15"` (date only)
- `"2024-01-15T10:30:00"` (ISO 8601)
- `"2024-01-15T10:30:00Z"` (with timezone)
- `"invalid-date"` (should fail)

### 5.2 Integration Tests for CsvSchemaScanner

**Test Cases:**
1. Simple CSV with all Text columns
2. CSV with mixed types (int, double, bool, date, string)
3. CSV with nullable columns (empty values)
4. CSV with header only (no data)
5. CSV with quoted fields
6. CSV with empty column names

**Example Test CSV:**
```csv
id,name,age,salary,active,created_at
1,Alice,30,50000.50,true,2024-01-15
```

Expected Schema (from first row):
- `id`: WholeNumber, not nullable
- `name`: Text, not nullable
- `age`: WholeNumber, not nullable
- `salary`: FloatingPoint, not nullable
- `active`: Boolean, not nullable
- `created_at`: Timestamp, not nullable

### 5.3 Benchmarks

**Scenarios:**
- Type inference on single value
- Schema scan on single row with multiple columns

**Metrics:**
- Zero allocations in `CsvTypeInferrer` methods
- Time to scan schema
- Memory usage

---

## 6. Acceptance Criteria

- [ ] Infers `WholeNumber` for integer values
- [ ] Infers `FloatingPoint` for decimal values
- [ ] Infers `Boolean` for true/false values (case-insensitive)
- [ ] Infers `Timestamp` for date/datetime values
- [ ] Infers `Text` as fallback (including empty values)
- [ ] Tracks nullable status correctly
- [ ] Zero allocations in `CsvTypeInferrer` (verified by benchmark)
- [ ] Unit tests for all type inference scenarios
- [ ] Integration tests for schema scanning
- [ ] No compiler warnings
- [ ] Code passes `dotnet format` validation

---

## 7. Dependencies

- **CsvDataRowIndexer**: For total row count
- **nietras.Sep**: For CSV parsing (existing dependency)

---

## 8. Future Work

### Phase 2: Custom Date Formats
- Support configurable date formats beyond ISO 8601
- Allow user-specified format patterns

### Phase 3: Type Coercion Hints
- Allow manual type overrides in schema
- Support "force string" option for specific columns

### Phase 4: Multi-Row Sampling (Optional)
- Configurable sampling (first N, random, distributed)
- Handle edge cases where first row is not representative

---

## 9. References

- RFC 4180: Common Format and MIME Type for CSV Files

---

## 10. Progressive Schema Refinement

This section describes the multi-row schema refinement system that allows accurate type inference by processing multiple rows. This addresses the limitation of single-row inference where null values or type changes appearing in later rows are not detected.

### 10.1 Problem Statement

Current single-row inference has limitations:
1. **Null values appear later**: A column might be non-null in the first row but have nulls later
2. **Type changes appear**: e.g., "123" (WholeNumber) in row 1, "123.45" (FloatingPoint) in row 2

### 10.2 Type Promotion Rules

When different types are observed for the same column across rows, types are "promoted" to a more general type.

**Type Promotion Matrix:**

| Current Type   | Observed Type   | Result         | Reason                                    |
|----------------|-----------------|----------------|-------------------------------------------|
| WholeNumber    | WholeNumber     | WholeNumber    | No change                                 |
| WholeNumber    | FloatingPoint   | FloatingPoint  | Integers are subset of floats             |
| WholeNumber    | Boolean         | Text           | Incompatible types                        |
| WholeNumber    | Timestamp       | Text           | Incompatible types                        |
| WholeNumber    | Text            | Text           | Text is universal fallback                |
| FloatingPoint  | WholeNumber     | FloatingPoint  | Integers can be represented as floats     |
| FloatingPoint  | FloatingPoint   | FloatingPoint  | No change                                 |
| FloatingPoint  | Boolean         | Text           | Incompatible types                        |
| FloatingPoint  | Timestamp       | Text           | Incompatible types                        |
| FloatingPoint  | Text            | Text           | Text is universal fallback                |
| Boolean        | Boolean         | Boolean        | No change                                 |
| Boolean        | Any other       | Text           | Incompatible types                        |
| Timestamp      | Timestamp       | Timestamp      | No change                                 |
| Timestamp      | Any other       | Text           | Incompatible types                        |
| Text           | Any             | Text           | Text absorbs all types                    |

**Key Design Decisions:**
1. **WholeNumber -> FloatingPoint is the only valid numeric promotion**: "123" then "123.45" results in `FloatingPoint`
2. **Boolean is NOT numeric**: "1" and "0" are `WholeNumber`, not `Boolean`. Only "true"/"false" are `Boolean`
3. **Text is the universal fallback**: Any incompatible type combination results in `Text`
4. **Nullability is one-way**: Once a column is marked nullable, it stays nullable

### 10.3 New Classes

#### ColumnTypeResolver (Static Class)

Pure type resolution logic. Only called when types differ.

```csharp
public static class ColumnTypeResolver
{
    /// <summary>
    /// Resolves two different types to their common supertype.
    /// </summary>
    /// <param name="current">The currently established type.</param>
    /// <param name="observed">The newly observed type.</param>
    /// <returns>The resolved type that can represent both.</returns>
    /// <remarks>
    /// Precondition: current != observed (caller should check before calling).
    /// </remarks>
    public static ColumnType Resolve(ColumnType current, ColumnType observed);
}
```

#### ColumnSchema (Modified to Mutable)

Change `ColumnSchema` from immutable to mutable for `Type` and `IsNullable` properties.

```csharp
public sealed record ColumnSchema
{
    public required string Name { get; init; }          // Immutable (set once)
    public required ColumnType Type { get; set; }       // Mutable (updated during scanning)
    public bool IsNullable { get; set; }                // Mutable (updated during scanning)
    public int ColumnIndex { get; init; }               // Immutable (set once)
    public string? DisplayFormat { get; init; }         // Immutable (set once)
}
```

**Rationale:**
- No "build" timing in streaming scenarios - schema evolves continuously
- `Name` and `ColumnIndex` are fixed at creation
- `Type` and `IsNullable` are refined as more rows are processed

#### ColumnSchemaExtensions (Extension Methods)

Extension methods for `ColumnSchema` to handle type updates with early-exit optimization.

```csharp
public static class ColumnSchemaExtensions
{
    /// <summary>
    /// Updates the column type if the observed type differs from current.
    /// </summary>
    /// <param name="schema">The column schema to update.</param>
    /// <param name="observedType">The newly observed type.</param>
    /// <remarks>
    /// If current type equals observed type, returns immediately (no-op).
    /// Otherwise, calls ColumnTypeResolver.Resolve() and updates the schema.
    /// </remarks>
    public static void UpdateColumnType(this ColumnSchema schema, ColumnType observedType)
    {
        if (schema.Type == observedType)
        {
            return; // Early exit - no change needed
        }

        schema.Type = ColumnTypeResolver.Resolve(schema.Type, observedType);
    }

    /// <summary>
    /// Marks the column as nullable if not already.
    /// </summary>
    public static void MarkNullable(this ColumnSchema schema)
    {
        schema.IsNullable = true;
    }
}
```

### 10.4 Usage Flow

**Processing a Value (Extension Method Flow):**
```
For each column value in a row:
  ↓
  if (empty/whitespace) → schema.MarkNullable(); continue;
  ↓
  var inferredType = CsvTypeInferrer.InferType(value);
  ↓
  schema.UpdateColumnType(inferredType);
      ↓
      if (schema.Type == inferredType) return;  // Early exit
      ↓
      schema.Type = ColumnTypeResolver.Resolve(schema.Type, inferredType);
```

### 10.5 Usage Examples

**Progressive Scanning (Direct Schema Update):**
```csharp
// Initial schema creation (e.g., from first row or header)
var schema = new TableSchema
{
    Columns = [
        new ColumnSchema { Name = "id", Type = ColumnType.WholeNumber, ColumnIndex = 0 },
        new ColumnSchema { Name = "price", Type = ColumnType.WholeNumber, ColumnIndex = 1 },
        new ColumnSchema { Name = "name", Type = ColumnType.Text, ColumnIndex = 2 },
    ],
    RowCount = 0,
    SourceFormat = DataFormat.Csv,
};

// Process Row 2: "456", "99.99", ""
schema.Columns[1].UpdateColumnType(ColumnType.FloatingPoint);  // WholeNumber -> FloatingPoint
schema.Columns[2].MarkNullable();  // Empty value

// Process Row 3: "", "200", "Bob"
schema.Columns[0].MarkNullable();  // Empty value
schema.Columns[1].UpdateColumnType(ColumnType.WholeNumber);    // No change (already FloatingPoint)

// Final state:
// id: WholeNumber, nullable=true
// price: FloatingPoint, nullable=false
// name: Text, nullable=true
```

**Integration with Existing CsvSchemaScanner:**
```csharp
// Existing single-row scan creates initial schema
var schemaResult = CsvSchemaScanner.ScanSchema(columnNames, firstRow, totalRowCount);
var schema = schemaResult.Value;

// Subsequent rows refine the schema using extension methods
foreach (var row in remainingRows)
{
    for (var i = 0; i < row.Count; i++)
    {
        var value = row[i].Span;
        if (CsvTypeInferrer.IsEmptyOrWhitespace(value))
        {
            schema.Columns[i].MarkNullable();
            continue;
        }

        var inferredType = CsvTypeInferrer.InferType(value);
        schema.Columns[i].UpdateColumnType(inferredType);
    }
}
```

### 10.6 Thread Safety

The current design is **NOT thread-safe** by default:
- `ColumnSchema.Type` and `ColumnSchema.IsNullable` are mutable properties
- Concurrent updates to the same `ColumnSchema` may cause race conditions

For future thread-safe scenarios, consider:
1. Lock-based synchronization when updating schema
2. Process rows in a single thread, or partition by column
3. Use immutable snapshots when reading schema during updates

### 10.7 Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/Engine/IO/ColumnTypeResolver.cs` | Create | Pure type resolution logic |
| `src/Engine/IO/ColumnSchemaExtensions.cs` | Create | Extension methods with early-exit optimization |
| `src/Engine/Models/ColumnSchema.cs` | Modify | Change `Type` and `IsNullable` from `init` to `set` |
| `tests/DataMorph.Tests/IO/ColumnTypeResolverTests.cs` | Create | Type resolution unit tests |
| `tests/DataMorph.Tests/IO/ColumnSchemaExtensionsTests.cs` | Create | Extension method unit tests |

### 10.8 Test Scenarios

**ColumnTypeResolver.Resolve():**
- WholeNumber + FloatingPoint -> FloatingPoint
- FloatingPoint + WholeNumber -> FloatingPoint
- Boolean + WholeNumber -> Text
- Timestamp + FloatingPoint -> Text
- Any + Text -> Text

**ColumnSchemaExtensions.UpdateColumnType():**
- Same type: returns early, no change
- Different type: calls Resolve and updates

**ColumnSchemaExtensions.MarkNullable():**
- Sets IsNullable = true
- Idempotent (calling twice has no additional effect)

### 10.9 Acceptance Criteria

- [ ] `ColumnSchema.Type` and `ColumnSchema.IsNullable` are mutable (`set` accessor)
- [ ] `ColumnTypeResolver.Resolve()` correctly resolves all type combinations per matrix
- [ ] `ColumnSchemaExtensions.UpdateColumnType()` returns early when types are equal
- [ ] `ColumnSchemaExtensions.MarkNullable()` sets `IsNullable = true`
- [ ] All unit tests pass
- [ ] No compiler warnings
- [ ] Code passes `dotnet format` validation
