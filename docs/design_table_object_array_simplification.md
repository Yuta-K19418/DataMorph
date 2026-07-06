# Design: Simplification Display Rule for Objects and Arrays in Table Mode

## 1. Requirements Recap

### Scope: this is a shared-primitive change, not Table-Mode-only

`JsonObjectCellExtractor.ExtractCell` is a shared Engine primitive, not a Table-Mode-only
helper. Confirmed callers beyond Table Mode's `JsonLinesTableSource`:

- `src/App/Views/FocusedTableSource.cs` — DrillDown's table view.
- `src/App/Cli/FilterEvaluator.cs` — CLI filter evaluation.
- `src/Engine/IO/JsonLines/FilterRowIndexer.cs` — background filter-row indexing, shared by
  Table Mode and DrillDown.
- `src/App/Cli/JsonLinesRecordReader.cs` — CLI export pipeline.

Changing `ExtractCell`'s output therefore changes what DrillDown renders and what the CLI
filter/export pipeline sees for nested columns too, not just the Table Mode grid. This is
intentional: `docs/functional_spec.md` §4 ("Selective Morphing") defines the simplification
rule as a general one, not scoped to a single view — this design implements it at the one
shared call site all views go through.

One consequence: `docs/design_drilldown_command_phase2.md` (its "Nested Values" section and its
test-plan row for "Nested object/array value in object row") documents and tests DrillDown's
nested-cell format as `{...}` / `[...]`. That documented format is superseded by this change.
No design changes to DrillDown itself are needed; this is purely a heads-up that its design
doc's prose/test-plan text should be read as historical from this point on.

### Revised format decision: reuse Tree View's exact wording

The initial cut of this design (see git history) rendered Objects as the bare literal
`{ Object }` (no count) and Arrays as `[ n items ]`. That asymmetry was reconsidered: an
object's property count is just as decision-relevant as an array's element count — e.g. it
previews how many columns a `Flatten` (`docs/functional_spec.md` §5.2) would produce, the same
way an array's count previews how many rows an `Explode` would produce. There's no good reason
to compute one and not the other.

Once both counts are in scope, the natural choice is to reuse the wording Tree View already
uses, rather than invent a second, Table-Mode-specific phrasing:

- **Objects**: `JsonObjectTreeNode.FormatDisplayText` (`src/App/Views/JsonTreeNodes/JsonObjectTreeNode.cs:111-158`)
  renders `{Object: N properties}`.
- **Arrays**: `JsonArrayTreeNode.FormatDisplayText` (`src/App/Views/JsonTreeNodes/JsonArrayTreeNode.cs:108-155`)
  renders `[Array: N items]`.

Table Mode will render the identical strings. This gives the app one consistent
"collapsed-container" presentation regardless of which view (Tree or Table) the user is in,
and lets the counting *and* formatting logic be fully shared — see section 3.
`docs/functional_spec.md` §4's illustrative example (`{ Object }` / `[ n items ]`, no counts,
plus a mention of `Skip()`) was stale against this change; updated in this same PR to
`{Object: N properties}` / `[Array: N items]` and to describe the depth-tracking count instead
of `Skip()`.

Updated requirements:

- **Primitives** (string, number, boolean, null): rendered as-is (no change).
- **Objects**: rendered as `{Object: N properties}`, where `N` is the top-level property count.
- **Arrays**: rendered as `[Array: N items]`, where `N` is the top-level element count.
- **Row height**: must stay fixed (single line). The current implementation already collapses
  nested values into a single-line string, so no change is needed here.
- **Performance**: must not parse deeper than necessary. Table Mode may format many cells per
  redraw, so each cell's cost must stay bounded to that cell's own byte range — never touch
  sibling cells or unrelated rows.

### Performance approach: depth-tracking loop, not `Skip()`

`Utf8JsonReader.Skip()` advances past a value but doesn't report how many children it
contained. Since both Object and Array now require a count, this design counts by reading
token-by-token and tracking nesting depth — the same technique already used (independently) by
`JsonObjectTreeNode.FormatDisplayText` and `JsonArrayTreeNode.FormatDisplayText` in Tree View.
The loop only reads the bytes belonging to the target value itself (bounded by its own
start/end tokens), so it never touches sibling cells or unrelated rows. This satisfies the
"don't parse unnecessarily deep" requirement while still producing an exact count. Table Mode
already pays an equivalent bounded-scan cost for arrays in the prior cut of this design, so
extending it to objects introduces no new cost *class*, only parity between the two.

## 2. Files to Change

| File | Change |
|---|---|
| `src/Engine/IO/Json/JsonByteExtractor.cs` | Add `CountObjectProperties` and `CountArrayElements` (depth-tracking counters), plus `FormatObjectPreview` and `FormatArrayPreview` thin wrappers that produce the shared `{Object: N properties}` / `[Array: N items]` strings (see section 3). |
| `src/Engine/IO/Json/JsonObjectCellExtractor.cs` | Change `FormatValue` to take `ref Utf8JsonReader` instead of `(JsonTokenType, ReadOnlySpan<byte>)`, so it can drive the reader forward to count. `StartObject`/`StartArray` branches delegate to the new `JsonByteExtractor` formatters. |
| `src/App/Views/JsonTreeNodes/JsonObjectTreeNode.cs` | Refactor `FormatDisplayText` to call `JsonByteExtractor.FormatObjectPreview` instead of its inline counting loop. Output text is unchanged. |
| `src/App/Views/JsonTreeNodes/JsonArrayTreeNode.cs` | Refactor `FormatDisplayText` to call `JsonByteExtractor.FormatArrayPreview` instead of its inline counting loop. Output text is unchanged. |
| `tests/DataMorph.Tests/Engine/IO/Json/JsonObjectCellExtractorTests.cs` | Update `ExtractCell_NestedObject_ReturnsCollapsedPreview` and `ExtractCell_Array_ReturnsCollapsedPreview` to expect the new format; add cases for an empty object, an empty array, and a container with nested children (to verify the depth-tracking count doesn't over/under-count). |
| `tests/DataMorph.Tests/Engine/IO/Json/JsonByteExtractorTests.cs` | Add tests for `CountObjectProperties`, `CountArrayElements`, `FormatObjectPreview`, and `FormatArrayPreview`. |
| `tests/DataMorph.Tests/Engine/IO/DrillDown/FullAggregationScannerTests.cs` | Update the two `JsonObjectCellExtractor.ExtractCell(...)` assertions (currently expecting `"{...}"` / `"[...]"` around lines 364-365) to the new format — this test calls the shared primitive directly and will regress otherwise. |
| `docs/functional_spec.md` | One-line update to §4's illustrative example and `Skip()` mention, now stale against the actual format/logic (done in this PR — see round-3 review). |

`JsonObjectTreeNodeTests` and `JsonArrayTreeNodeTests` need no changes — both refactors are
behavior-preserving (same output strings), so the existing assertions continue to hold. Re-run
both suites to confirm.

## 3. Implementation Outline

### 3.1 Shared counters and formatters (Engine layer)

`JsonByteExtractor` (`src/Engine/IO/Json/JsonByteExtractor.cs`) already exists specifically to
hold JSON traversal primitives shared between the Engine layer and the App-layer tree view code
(its own doc comment says as much). Add two counters and two formatting wrappers, and update
the class-level doc comment (currently written only in terms of `ExtractNestedBytes`) to
describe all of the traversal primitives it now holds:

```csharp
/// <summary>
/// Counts the top-level properties of a JSON object by tracking brace/bracket depth.
/// The reader must be positioned at a <see cref="JsonTokenType.StartObject"/> token; on return
/// the reader has consumed the entire object, ending at its matching EndObject token.
/// </summary>
public static int CountObjectProperties(ref Utf8JsonReader reader)
{
    var propertyCount = 0;
    var depth = 1;

    while (depth > 0 && reader.Read())
    {
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            depth++;
            continue;
        }

        if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
        {
            depth--;
        }

        if (depth == 1 && reader.TokenType == JsonTokenType.PropertyName)
        {
            propertyCount++;
        }
    }

    return propertyCount;
}

/// <summary>
/// Counts the top-level elements of a JSON array by tracking bracket/brace depth.
/// The reader must be positioned at a <see cref="JsonTokenType.StartArray"/> token; on return
/// the reader has consumed the entire array, ending at its matching EndArray token.
/// </summary>
public static int CountArrayElements(ref Utf8JsonReader reader)
{
    var elementCount = 0;
    var depth = 1;

    while (depth > 0 && reader.Read())
    {
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            depth++;
            continue;
        }

        if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
        {
            depth--;
        }

        if (depth == 1)
        {
            elementCount++;
        }
    }

    return elementCount;
}

/// <summary>
/// Formats a JSON object's collapsed preview text (e.g. "{Object: 3 properties}"). The reader
/// must be positioned at a <see cref="JsonTokenType.StartObject"/> token.
/// </summary>
public static string FormatObjectPreview(ref Utf8JsonReader reader)
{
    var propertyCount = CountObjectProperties(ref reader);
    return FormattableString.Invariant($"{{Object: {propertyCount:N0} properties}}");
}

/// <summary>
/// Formats a JSON array's collapsed preview text (e.g. "[Array: 3 items]"). The reader must be
/// positioned at a <see cref="JsonTokenType.StartArray"/> token.
/// </summary>
public static string FormatArrayPreview(ref Utf8JsonReader reader)
{
    var elementCount = CountArrayElements(ref reader);
    return FormattableString.Invariant($"[Array: {elementCount:N0} items]");
}
```

`CountObjectProperties` and `CountArrayElements` are the same algorithms already used inline by
`JsonObjectTreeNode`/`JsonArrayTreeNode`, moved to the Engine layer. `FormatObjectPreview` /
`FormatArrayPreview` centralize the exact wording so it's defined in exactly one place instead
of drifting across three call sites (Table Mode cell extraction, Tree View object nodes, Tree
View array nodes).

### 3.2 `JsonObjectCellExtractor.FormatValue`

Change the signature from `FormatValue(JsonTokenType tokenType, ReadOnlySpan<byte> valueSpan)`
to `FormatValue(ref Utf8JsonReader reader)`, and update the call site in `ExtractCell`
(currently `return FormatValue(reader.TokenType, reader.ValueSpan);`) to
`return FormatValue(ref reader);`. This is required because counting means advancing the reader
past the `StartObject`/`StartArray` token, which the current by-value parameters don't allow.

```csharp
private static string FormatValue(ref Utf8JsonReader reader)
{
    if (reader.TokenType == JsonTokenType.Number)
    {
        return FormatNumber(reader.ValueSpan);
    }

    return reader.TokenType switch
    {
        JsonTokenType.String => Encoding.UTF8.GetString(reader.ValueSpan),
        JsonTokenType.True => "True",
        JsonTokenType.False => "False",
        JsonTokenType.Null => "<null>",
        JsonTokenType.StartObject => JsonByteExtractor.FormatObjectPreview(ref reader),
        JsonTokenType.StartArray => JsonByteExtractor.FormatArrayPreview(ref reader),
        _ => "<null>",
    };
}
```

### 3.3 `JsonObjectTreeNode.FormatDisplayText` / `JsonArrayTreeNode.FormatDisplayText` refactor

Both keep their existing "is this actually a valid Object/Array start" guard, then replace the
inline counting loop with a single delegated call:

```csharp
// JsonObjectTreeNode
return JsonByteExtractor.FormatObjectPreview(ref reader);

// JsonArrayTreeNode
return JsonByteExtractor.FormatArrayPreview(ref reader);
```

The reader is already positioned at `StartObject`/`StartArray` at this point (same precondition
the current inline loops assume), so no other change is needed. Output text is unchanged, so
neither `JsonObjectTreeNodeTests` nor `JsonArrayTreeNodeTests` require updates.

## 4. Test Plan

- `JsonByteExtractorTests`: new cases for `CountObjectProperties` (empty object → 0, flat object
  → property count, object with a nested object/array value → nested keys not counted), mirror
  cases for `CountArrayElements` (empty array → 0, flat array, array with nested
  object/array elements), and cases for `FormatObjectPreview` / `FormatArrayPreview` confirming
  the exact rendered string.
- `JsonObjectCellExtractorTests`: update the two existing collapsed-preview tests to the new
  strings (`"{Object: 1 properties}"` for `{"city": "Tokyo"}`, `"[Array: 2 items]"` for
  `["a", "b"]`); add an empty-object case, an empty-array case, and a case with a nested
  container inside the array/object value to confirm the count reflects only direct children.
- `JsonObjectTreeNodeTests` / `JsonArrayTreeNodeTests`: no changes expected (output format is
  unchanged); re-run existing suites to confirm the refactor is behavior-preserving.
- `FullAggregationScannerTests`: update the two `ExtractCell` assertions — `"{...}"` →
  `"{Object: 1 properties}"` (the `address` object has one key, `city`), `"[...]"` →
  `"[Array: 2 items]"` (the `tags` array has two elements).
