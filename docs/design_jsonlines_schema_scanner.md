# JSON Lines Schema Scanner - Design Document

## Scope

Implements the **Schema Scanner** for JSON Lines format as a prerequisite for the Table Mode
Virtual Viewport feature. The scanner dynamically infers a `TableSchema` from the first N lines
of a JSON Lines file by performing a union of all observed keys and their types.

---

## 1. Requirements

### Functional Requirements

- Perform **dynamic union** of JSON object keys across the first N lines (default: 200)
- **Infer types** per key using `Utf8JsonReader` token types (zero allocation)
- Support **incremental column discovery** via `RefineSchema` during viewport scrolling
- Mark columns as nullable when:
  - The key is absent in some rows
  - The value is JSON `null` in some rows
- Map nested objects (`{ }`) to `ColumnType.JsonObject`
- Map arrays (`[ ]`) to `ColumnType.JsonArray`
- Preserve **first-seen key order** to maintain stable column ordering

### Non-Functional Requirements

- **Zero allocation** in parsing hot paths (use `Utf8JsonReader` directly on `ReadOnlySpan<byte>`)
- **Native AOT compatible** (no reflection, no `System.Reflection.Emit`)
- All methods return `Result<T>` for expected failures (no exceptions for control flow)
- Target under 300 lines per class (Single Responsibility Principle)

---

## 2. Design

### 2.1 `ColumnType` Enum Extension

Two new members are added at the end of the existing `ColumnType` enum.

```csharp
/// <summary>
/// JSON object value ({ ... }).
/// </summary>
JsonObject,

/// <summary>
/// JSON array value ([ ... ]).
/// </summary>
JsonArray,
```

#### Naming Decision: `JsonObject`/`JsonArray` vs `Object`/`Array` vs `Json`

During design review, three naming options were considered:

| Option | Example | Assessment |
|---|---|---|
| `Object` / `Array` | `ColumnType.Object` | Ambiguous — `Object` clashes with `System.Object` conceptually |
| `Json` (single member) | `ColumnType.Json` | Simple, but loses granularity between object and array shapes |
| **`JsonObject` / `JsonArray`** | `ColumnType.JsonObject` | **Selected** — explicit, self-documenting, no ambiguity |

**Decision:** Use `ColumnType.JsonObject` and `ColumnType.JsonArray` for clarity.

---

### 2.2 `ColumnTypeResolver` Extension

A block is inserted **after** the existing `Text` check and **before** the numeric promotion block.

```csharp
// Structured JSON types: same-type is stable; any other combination → Text
if (current == ColumnType.JsonObject || current == ColumnType.JsonArray ||
    observed == ColumnType.JsonObject || observed == ColumnType.JsonArray)
{
    return ColumnType.Text;
}
```

**Resolution matrix for new members:**

| Current         | Observed        | Result  | Reason                                          |
|-----------------|-----------------|---------|-------------------------------------------------|
| `JsonObject`    | `JsonObject`    | `JsonObject` | Same type — handled by early-return guard  |
| `JsonArray`     | `JsonArray`     | `JsonArray`  | Same type — handled by early-return guard  |
| `JsonObject`    | `JsonArray`     | `Text`  | Mixed structured types → universal fallback      |
| `JsonArray`     | `JsonObject`    | `Text`  | Mixed structured types → universal fallback      |
| `JsonObject`    | `WholeNumber`   | `Text`  | Structured + scalar → universal fallback         |
| `JsonObject`    | `FloatingPoint` | `Text`  | Structured + scalar → universal fallback         |
| `JsonObject`    | `Boolean`       | `Text`  | Structured + scalar → universal fallback         |
| `JsonObject`    | `Timestamp`     | `Text`  | Structured + scalar → universal fallback         |
| `JsonObject`    | `Text`          | `Text`  | Already handled by prior `Text` check            |
| `JsonArray`     | (any scalar)    | `Text`  | Same logic as `JsonObject` rows above            |
| `WholeNumber`   | `JsonObject`    | `Text`  | Scalar + structured → universal fallback         |
| `Text`          | `JsonObject`    | `Text`  | Already handled by prior `Text` check            |

> **Design note:** The plan document contained a test scenario table with
> `"Json + WholeNumber conflict → Json"`, which was inconsistent with the description
> (`"Any Object or Array combined with a scalar also falls back to Text"`).
> After review, the **description** is treated as authoritative because it is more explicit
> and because Text-as-universal-fallback is a consistent, simpler invariant.
> The test suite reflects the description: `JsonObject + WholeNumber → Text`.

---

### 2.3 Class: `SchemaScanner` (JsonLines)

**File:** `src/Engine/IO/JsonLines/SchemaScanner.cs`
**Namespace:** `DataMorph.Engine.IO.JsonLines`

Static class with two public methods and two private helpers.

#### API

```csharp
/// <summary>
/// Scans the first N lines of a JSON Lines file and returns an inferred schema.
/// Performs a dynamic union of all observed keys and their types.
/// </summary>
/// <param name="lineBytes">Raw byte buffers of individual lines.</param>
/// <param name="initialScanCount">Maximum number of lines to scan (default: 200).</param>
/// <returns>Result containing the inferred TableSchema, or an error message.</returns>
public static Result<TableSchema> ScanSchema(
    IReadOnlyList<ReadOnlyMemory<byte>> lineBytes,
    int initialScanCount = 200);

/// <summary>
/// Refines an existing schema with one additional line.
/// Used for incremental column discovery during viewport scrolling.
/// Existing column types are updated via ColumnTypeResolver.
/// New keys discovered in this line are added with IsNullable = true
/// (they were absent in all previous rows).
/// Existing columns absent in this line are marked nullable.
/// </summary>
/// <param name="schema">The schema to refine.</param>
/// <param name="lineBytes">Raw bytes of a single JSON Lines row.</param>
/// <returns>Result containing the updated TableSchema, or the original schema if the line is malformed.</returns>
public static Result<TableSchema> RefineSchema(
    TableSchema schema,
    ReadOnlySpan<byte> lineBytes);
```

#### Private Helpers

```csharp
/// <summary>
/// Reads the current value token from a Utf8JsonReader and returns the inferred ColumnType.
/// Caller must ensure the token is NOT JsonTokenType.Null before calling (use IsNullToken first).
/// Caller is responsible for calling reader.Skip() on StartObject/StartArray before calling this.
/// Returns ColumnType.Text as fallback for any unexpected tokens.
/// </summary>
private static ColumnType InferType(ref Utf8JsonReader reader);

/// <summary>
/// Returns true if the current reader token is JSON null.
/// Mirrors the role of TypeInferrer.IsEmptyOrWhitespace() in the CSV pipeline.
/// </summary>
private static bool IsNullToken(ref Utf8JsonReader reader);

/// <summary>
/// Parses one JSON object line and updates the mutable maps in-place.
/// </summary>
/// <param name="line">Raw bytes of a single JSON object line.</param>
/// <param name="columnMap">key → currently inferred ColumnType (mutated in-place).</param>
/// <param name="keyOrder">Ordered list of keys (first-seen order); new keys are appended.</param>
/// <param name="columnObservedCount">Counts of non-null observations per key (mutated in-place).</param>
/// <returns>Success, or Failure if the line is not a valid JSON object.</returns>
private static Result ScanLine(
    ReadOnlySpan<byte> line,
    Dictionary<string, ColumnType> columnMap,
    List<string> keyOrder,
    Dictionary<string, int> columnObservedCount);
```

---

### 2.4 Type Inference Rules

This design mirrors the CSV pipeline pattern:

| CSV                          | JSON Lines equivalent                   |
|------------------------------|-----------------------------------------|
| `TypeInferrer.IsEmptyOrWhitespace(span)` | `IsNullToken(ref reader)` — checks for `JsonTokenType.Null` |
| `TypeInferrer.InferType(span)` | `InferType(ref reader)` — always returns `ColumnType` |

**`IsNullToken`** — call this first (before `InferType`):

| Condition                  | Returns |
|----------------------------|---------|
| `TokenType == Null`        | `true`  |
| Any other token            | `false` |

**`InferType`** — only called when `IsNullToken` returns `false`:

| JSON token / condition        | Returns         | Notes                                  |
|-------------------------------|-----------------|----------------------------------------|
| `Number` + `TryGetInt64()` ✓  | `WholeNumber`   | Integer fits in 64-bit                 |
| `Number` + `TryGetInt64()` ✗  | `FloatingPoint` | Decimal or large number                |
| `String`                      | `Text`          | Any quoted string                      |
| `True` or `False`             | `Boolean`       | JSON boolean literals                  |
| `StartObject`                 | `JsonObject`    | Nested `{ }` — caller calls `Skip()`  |
| `StartArray`                  | `JsonArray`     | Nested `[ ]` — caller calls `Skip()`  |
| Any other token               | `Text`          | Unexpected token; safe fallback        |

---

### 2.5 Dynamic Union Algorithm (`ScanSchema`)

```
State:
  columnMap:            Dictionary<string, ColumnType>   // key → inferred type
  keyOrder:             List<string>                     // preserves first-seen order
  columnObservedCount:  Dictionary<string, int>          // counts of non-null observations
  rowsScanned:          int                              // total valid rows processed

For each line (up to initialScanCount):
  1. Call ScanLine(line, columnMap, keyOrder, columnObservedCount)
  2. If ScanLine fails → skip (malformed or non-object line)
  3. If ScanLine succeeds → rowsScanned++

Inside ScanLine for each key-value pair:
  a. Read property name
  b. Read value token
  c. If token is StartObject or StartArray:
       → Call reader.Skip() to advance past the nested structure
       → inferredType = JsonObject or JsonArray (set directly, skip IsNullToken/InferType)
       → Update columnMap and increment columnObservedCount[key]

  d. Else if IsNullToken(ref reader):
       → JSON null: do NOT change type, do NOT increment columnObservedCount
       → If key is new: append to keyOrder, columnMap[key] = ColumnType.Text (default)
       → (observedCount stays 0 → will be flagged nullable after all rows)

  e. Else (non-null scalar):
       → inferredType = InferType(ref reader)   // always returns ColumnType
       → If key is new: append to keyOrder, columnMap[key] = inferredType
       → If key exists: columnMap[key] = ColumnTypeResolver.Resolve(columnMap[key], inferredType)
       → columnObservedCount[key]++

After all rows:
  For each key in keyOrder:
    IsNullable = columnObservedCount[key] < rowsScanned

Build TableSchema:
  columns = keyOrder.Select((key, idx) => new ColumnSchema {
    Name         = key,
    Type         = columnMap[key],
    IsNullable   = columnObservedCount.GetValueOrDefault(key) < rowsScanned,
    ColumnIndex  = idx,
    DisplayFormat = null
  })
  return Results.Success(new TableSchema { Columns = columns, SourceFormat = DataFormat.JsonLines })
```

---

### 2.6 Incremental Refinement Algorithm (`RefineSchema`)

```
1. Build mutable state from existing schema:
     columnMap  ← schema.Columns (key → type)
     keyOrder   ← schema.Columns (ordered names)
     columnObservedCount ← fresh empty dictionary

2. Call ScanLine(lineBytes, columnMap, keyOrder, columnObservedCount)
   - If ScanLine fails → return original schema unchanged (malformed line)

3. Build updatedColumns from keyOrder:
   For each key:
     a. If key existed in original schema:
          updatedType     = columnMap[key]                   // may have been resolved by ScanLine
          isNullable      = original.IsNullable
                          || !columnObservedCount.ContainsKey(key)  // absent or null in this row
          → Return updated ColumnSchema (CoW — only allocate if changed)

     b. If key is new (not in original schema):
          type       = columnMap[key]
          isNullable = true   // was absent in ALL previous rows
          → Return new ColumnSchema with ColumnIndex = position in keyOrder

4. Return Results.Success(schema with { Columns = updatedColumns })
```

---

### 2.7 Nullable Detection Logic

A column is marked **nullable** when:

| Cause                           | Detection mechanism                                 |
|---------------------------------|-----------------------------------------------------|
| Key absent from a row           | `columnObservedCount[key] < rowsScanned`            |
| JSON `null` value in a row      | `IsNullToken` returns `true`; observedCount not incremented → same as absent |
| New key in `RefineSchema`       | Always `IsNullable = true` (absent in previous rows)|
| Existing key absent in `RefineSchema` | `!columnObservedCount.ContainsKey(key)` → `IsNullable = true` |

Once nullable, a column stays nullable (one-way flag).

---

## 3. Edge Cases

| Scenario                             | Handling                                                                |
|--------------------------------------|-------------------------------------------------------------------------|
| Empty input (`lineBytes.Count == 0`) | `Results.Failure("No lines provided for schema inference")`            |
| All lines are malformed              | `rowsScanned == 0` → `Results.Failure("No valid JSON objects found…")` |
| Line is not a JSON object (array/scalar) | `ScanLine` returns Failure → line is skipped                       |
| Empty JSON object `{}`               | Valid row; all existing columns get no observedCount increment → nullable|
| Key appears only in later rows       | Nullable = true (absent in earlier rows)                               |
| First observation of key is null     | Initialized as `Text`; `IsNullable = true`                             |
| Nested object `{"a": {"b": 1}}`      | `"a"` → `ColumnType.JsonObject`; inner keys are NOT flattened          |
| Nested array `{"tags": [1,2,3]}`     | `"tags"` → `ColumnType.JsonArray`                                      |
| Mixed: `{"x": {}}` then `{"x": []}` | `ColumnTypeResolver.Resolve(JsonObject, JsonArray)` → `Text`            |
| Mixed: `{"x": 42}` then `{"x": {}}`  | `ColumnTypeResolver.Resolve(WholeNumber, JsonObject)` → `Text`         |
| `initialScanCount = 0`              | Zero rows scanned → `Results.Failure("No valid JSON objects found…")`  |
| Negative `initialScanCount`         | `ArgumentOutOfRangeException` (guard clause)                           |

---

## 4. Files to Create / Modify

| File | Action | Purpose |
|---|---|---|
| `src/Engine/Types/ColumnType.cs` | Modify | Add `JsonObject`, `JsonArray` members |
| `src/Engine/IO/ColumnTypeResolver.cs` | Modify | Handle `JsonObject`/`JsonArray` |
| `src/Engine/IO/JsonLines/SchemaScanner.cs` | **Create** | Schema scanning implementation |
| `tests/DataMorph.Tests/Engine/IO/JsonLines/SchemaScannerTests.cs` | **Create** | Base test class |
| `tests/DataMorph.Tests/Engine/IO/JsonLines/SchemaScannerTests.ScanSchema.cs` | **Create** | ScanSchema test cases |
| `tests/DataMorph.Tests/Engine/IO/JsonLines/SchemaScannerTests.RefineSchema.cs` | **Create** | RefineSchema test cases |
| `docs/design_jsonlines_schema_scanner.md` | **Create** | This document |

### Reused Existing Components

| Component | Path | Usage |
|---|---|---|
| `ColumnTypeResolver.Resolve()` | `src/Engine/IO/ColumnTypeResolver.cs` | Type conflict resolution |
| `ColumnSchemaExtensions` | `src/Engine/IO/ColumnSchemaExtensions.cs` | Copy-on-Write schema updates |
| `Result<T>` / `Results` | `src/Engine/Result.cs`, `Results.cs` | Error handling |
| `TableSchema`, `ColumnSchema` | `src/Engine/Models/` | Output types |

---

## 5. Test Scenarios

### 5.1 `ScanSchema` Tests

| Scenario | Expected |
|---|---|
| Single line, all primitives | Correct types, `IsNullable = false` |
| Multi-line, consistent types | Types preserved across rows |
| Missing key in some rows | `IsNullable = true` for that column |
| New key added in a later row | Column added, `IsNullable = true` (absent in earlier rows) |
| Nested object `{}` | `ColumnType.JsonObject` |
| Array `[]` | `ColumnType.JsonArray` |
| `WholeNumber` + `FloatingPoint` conflict | `FloatingPoint` (numeric promotion) |
| `WholeNumber` + `Boolean` conflict | `Text` (incompatible types) |
| `JsonObject` + `WholeNumber` conflict | `Text` (structured + scalar → fallback) |
| `JsonObject` + `JsonArray` conflict | `Text` (mixed structured types → fallback) |
| `JsonObject` + `Text` conflict | `Text` (Text is universal fallback) |
| JSON `null` value in row | Type unchanged, `IsNullable = true` |
| Empty input | `Result.Failure` |
| All lines malformed | `Result.Failure` |
| `initialScanCount = 0` | `Result.Failure` |
| Negative `initialScanCount` | `ArgumentOutOfRangeException` |

### 5.2 `RefineSchema` Tests

| Scenario | Expected |
|---|---|
| New key in refinement line | Added as new column, `IsNullable = true` |
| Existing column absent in line | `IsNullable = true` on that column |
| Type conflict in refinement | Resolved via `ColumnTypeResolver` |
| Malformed JSON line | Original schema returned unchanged |
| Empty span | Original schema returned unchanged |

---

## 6. Acceptance Criteria

- [ ] `ColumnType.JsonObject` and `ColumnType.JsonArray` added to enum
- [ ] `ColumnTypeResolver.Resolve()` handles all `JsonObject`/`JsonArray` combinations correctly
- [ ] `ScanSchema` infers correct types from multi-line JSON Lines input
- [ ] `ScanSchema` marks nullable when keys are absent or null
- [ ] `RefineSchema` adds new columns with `IsNullable = true`
- [ ] `RefineSchema` marks existing absent columns nullable
- [ ] All edge cases in Section 3 handled correctly
- [ ] `dotnet test` passes with zero failures
- [ ] `dotnet format` produces no diff
- [ ] Zero compiler warnings (`TreatWarningsAsErrors = true`)
