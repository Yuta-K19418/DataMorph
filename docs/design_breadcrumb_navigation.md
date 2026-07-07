# Breadcrumb Navigation — Design Document

## Terminology Note

The source requirement refers to a "Morph Command" that converts Tree → Table. No such command
exists in this codebase; the only Tree → Table conversion is the **DrillDown** command (`x` key,
`SingleDrillDownRequest` / `FullAggregationDrillDownRequest`). This document treats every mention
of "morph"/"Morph Command" in the requirement as DrillDown.

## Scope

Displays the current location in the JSON hierarchy as a breadcrumb bar at the top of the TUI
(directly below the MenuBar), and keeps it updated as the user navigates.

| Mode | In scope | Reason |
|---|---|---|
| `JsonLinesTree` / `JsonArrayTree` / `JsonObjectTree` | ✅ | Cursor position changes the path in real time |
| `FocusedTable` (DrillDown result) | ✅ | The table is scoped to a KeyPath; that path is worth surfacing |
| `JsonLinesTable` / `JsonArrayTable` (`t`-key toggle) | ❌ | Re-renders already-loaded rows with no scan and no scoped path (always root-equivalent); `JsonArrayTable` is currently a stub |
| `CsvTable` | ❌ | No hierarchical structure |

**Out of scope (deferred):**
- Clicking a **FocusedTable** breadcrumb segment to jump back into Tree mode at that ancestor
  level. This needs a "return to Tree and restore selection at an arbitrary ancestor" mechanism,
  which `docs/design_drilldown_command_phase2.md` already deferred under "Back / Undo navigation
  (Backspace to return to Tree)". Building that is a separate, non-trivial feature and should be
  tracked as its own follow-up rather than bundled here.
- Exhaustive mouse-support verification across terminal emulators. Click handling is implemented
  on a best-effort basis using Terminal.Gui's standard mouse API; not every terminal/driver
  combination will be tested.

---

## 1. Existing Infrastructure (Reused)

- `KeyPathSegment` / `KeyPathSegmentKind` — `src/Engine/IO/DrillDown/KeyPathSegment.cs`. Tags a
  segment as `Key` (object property) or `Index` (array element), avoiding ambiguity with literal
  keys such as `"[0]"`.
- `AppKeyHandler.BuildKeyPath(ITreeNode)` — `src/App/AppKeyHandler.cs:314`. Walks the
  `ParentNode` chain of a selected tree node up to the root, producing an ordered
  `IReadOnlyList<KeyPathSegment>`. Currently only called when DrillDown is triggered
  (`HandleFullAggregationDrillDown`).
- `FullAggregationDrillDownRequest.KeyPath` — already carries this list into the scanner.

## 2. Gaps

- `AppState` (`src/App/AppState.cs`) has no field for "the path currently on screen."
- `DrillDownState` (`src/App/DrillDownState.cs`) holds only `Rows`/`Schema` — no KeyPath.
- No tree view wires `TreeView.SelectionChanged`; cursor movement (both vim keys via
  `AdjustSelection` and native arrow keys) is not observed anywhere today.
- `MainWindow` only ever adds a `MenuBar` (row 0) and `StatusBar` (bottom); every
  `ViewManager.SwitchTo*` method hardcodes its content view to `Y=1, Height=Dim.Fill()-1`
  ("start below MenuBar, leave the bottom row for StatusBar"). There is no row reserved for a
  breadcrumb bar and no shared container to place one without touching 6 call sites individually.

## 3. Design

### 3.1 State: `AppState.CurrentKeyPath`

Add one field, parallel to the existing `CurrentFilePath`/`CurrentMode`:

```csharp
public IReadOnlyList<KeyPathSegment> CurrentKeyPath { get; set; } = [];
```

Only meaningful while `CurrentMode` is a Tree mode or `FocusedTable`. Updated in two places:

- **Tree modes**: on every `SelectionChanged` (see 3.3).
- **FocusedTable**: once, at the moment DrillDown is triggered (see 3.4).

### 3.2 Extracting `KeyPathBuilder`

`AppKeyHandler.BuildKeyPath` moves to a new static class `src/App/KeyPathBuilder.cs`
(`KeyPathBuilder.Build(ITreeNode node)`), with `AppKeyHandler` updated to call it.

Reason: the new call site for KeyPath-building is not `ViewManager` but the tree view `Create`
factories (`JsonLinesTreeView.Create`, `JsonArrayTreeView.Create`, `JsonObjectTreeView.Create`,
all in `App.Views`) — see 3.3, where the `onSelectionChanged` lambda calls
`KeyPathBuilder.Build(node)` directly. A static call from `App.Views` into `AppKeyHandler`
(`App`) is not itself a new layering violation — `MorphTreeView.cs` and `MorphTableView.cs`
already call `AppKeyHandler.IsGlobalShortcut` the same way. The reason to extract is narrower:
`AppKeyHandler` is a keyboard-shortcut handler, and `BuildKeyPath`/`KeyPathBuilder` is unrelated
to that responsibility — carrying it along as a static utility on `AppKeyHandler` was already a
minor responsibility leak before this feature, and this is a natural point to split it out rather
than add a second unrelated caller to it. Existing tests in `AppKeyHandlerTests.cs`
(`BuildKeyPath_WithRootSelection_ReturnsEmptyKeyPath`,
`BuildKeyPath_WithNestedObjectArraySelection_ReturnsOrderedSegmentsWithIndex`) move to a new
`KeyPathBuilderTests.cs` unchanged except for the type name.

### 3.3 Live updates in Tree modes

`MorphTreeView` (`src/App/Views/MorphTreeView.cs`) gains a constructor parameter
`Action<ITreeNode?> onSelectionChanged`, wired once in the constructor:

```csharp
SelectionChanged += (_, _) => onSelectionChanged(SelectedObject is ITreeNode node ? node : null);
```

Both vim-key navigation (`AdjustSelection`, called directly from `MorphTreeView.OnKeyDown`) and
native arrow-key navigation update `SelectedObject` internally, and both reliably raise
`SelectionChanged` — so a single subscription covers every navigation path, with no need for a
separate hook on `AdjustSelection`.

`JsonLinesTreeView.Create`, `JsonArrayTreeView.Create`, `JsonObjectTreeView.Create` each gain an
`onPathChanged` parameter (`Action<IReadOnlyList<KeyPathSegment>>`) and thread through:

```csharp
onSelectionChanged: node => onPathChanged(node is null ? [] : KeyPathBuilder.Build(node))
```

`JsonLinesTreeView` and `JsonArrayTreeView` do not extend `MorphTreeView` directly — both extend
`RangeTreeViewBase` (`src/App/Views/RangeTreeViewBase.cs`), which itself extends `MorphTreeView`
and currently forwards only `onTableModeToggle` to `base(...)`. `RangeTreeViewBase`'s constructor
must also accept `onSelectionChanged` and forward it to `base(...)` for the parameter to reach
`MorphTreeView` from these two view types. `JsonObjectTreeView` extends `MorphTreeView` directly,
so it needs no intermediate change.

`ViewManager.SwitchToJsonLinesTree/JsonArrayTree/JsonObjectTree` pass a call to
`UpdateBreadcrumb(path, collapseIndices: false)` (3.5) as `onPathChanged`, and invoke it once
immediately after the view is built so the breadcrumb reflects the initial selection instead of
staying blank until the first cursor move.

### 3.4 Static path for FocusedTable

`SingleDrillDownRequest` gains a `KeyPath` field for symmetry with
`FullAggregationDrillDownRequest`, computed the same way at trigger time:

```csharp
// AppKeyHandler.HandleSingleDrillDown
var request = new SingleDrillDownRequest(
    Format: format,
    NodeBytes: arrayNode.RawJson,
    KeyPath: KeyPathBuilder.Build(selectedNode));
```

`ViewManager.DrillDown` (Phase 1, `SingleDrillDownRequest`) calls
`UpdateBreadcrumb(request.KeyPath, collapseIndices: false)`, and
`FullAggregationDrillDownAsync` (Phase 2, `FullAggregationDrillDownRequest`) calls
`UpdateBreadcrumb(request.KeyPath, collapseIndices: true)` — `request` (the method's own
parameter) is captured by the `_uiThreadInvoke` callback, so no new field is needed on
`DrillDownState` to carry `KeyPath` through — both right before
`SwitchToFocusedTable` — so the path captured at the exact node the user triggered DrillDown from
becomes the FocusedTable's breadcrumb and stays fixed for the session (matching "Store morph path
when converting Tree → Table"). See 3.5 for why the two DrillDown variants need different
`collapseIndices` values despite sharing one `ViewMode.FocusedTable`.

### 3.5 Rendering: `BreadcrumbBar` + index-collapsing rule

New `src/App/Views/BreadcrumbBar.cs`, a small `View` (single row, `Y=1` — see 3.6 for exact
placement) that renders a formatted path string and exposes:

```csharp
internal void SetPath(IReadOnlyList<KeyPathSegment> path, bool collapseIndices);
internal event Action<int>? SegmentActivated; // clicked segment's 0-based index
```

Formatting is delegated to a new static `src/App/KeyPathFormatter.cs`
(`Format(IReadOnlyList<KeyPathSegment> path, bool collapseIndices)` → e.g. `"root → data →
orders[*]"`), which also returns the column range of each rendered segment so `BreadcrumbBar` can
map a mouse click's X position back to a segment index.

**`collapseIndices` decision:** per `docs/design_drilldown_command_phase2.md` §1.3, discarding the
specific array index and expanding every element (`orders` and `orders[0]` produce identical
output) is a rule that applies **only to Phase 2 — `FullAggregationDrillDownRequest`** (JSON
Lines / JSON Array, full-file scan). **Phase 1 — `SingleDrillDownRequest`** (JSON Object) does no
such thing: `ModeController.DrillDown` calls `DrillDownSchemaExtractor.ExtractFromNode` once,
against the one concrete node the user selected, with no index-discarding and no file-wide
expansion. `ViewMode.FocusedTable` is a single mode shared by both, so `_state.CurrentMode ==
ViewMode.FocusedTable` cannot distinguish them — deriving `collapseIndices` from `CurrentMode`
alone would render a Phase 1 result such as `list[2].orders` as `list[*] → orders`, falsely
implying the table aggregates every element of `list` when it is actually scoped to element 2
only.

So `collapseIndices` must instead be decided **at the point each request is built**, based on
request type, not read back from `CurrentMode` later:

- **Tree modes** (live navigation, 3.3): `collapseIndices = false` — the concrete index (e.g.
  `[2]`) is shown; the cursor is genuinely on one specific element.
- **`SingleDrillDownRequest` → FocusedTable** (3.4, Phase 1): `collapseIndices = false` — the
  path is scoped to the one selected node, same as tree mode.
- **`FullAggregationDrillDownRequest` → FocusedTable** (3.4, Phase 2): `collapseIndices = true` —
  every `Index` segment renders as `[*]`, reflecting the actual expand-all scan semantics.

`ViewManager.UpdateBreadcrumb` therefore takes `collapseIndices` as an explicit parameter rather
than inferring it:

```csharp
internal void UpdateBreadcrumb(IReadOnlyList<KeyPathSegment> path, bool collapseIndices)
{
    _state.CurrentKeyPath = path;
    _breadcrumbBar.SetPath(path, collapseIndices);
}
```

Call sites: tree `onPathChanged` callbacks and `ViewManager.DrillDown` (Phase 1) pass `false`;
`FullAggregationDrillDownAsync` (Phase 2) passes `true`. No new field on `AppState` or
`DrillDownState` is needed to track "which DrillDown variant produced this FocusedTable" — the
call site already knows, at the point it has the concrete request in hand. For the two
out-of-scope table modes and `FileSelection`, the bar is cleared (`SetPath([], collapseIndices:
false)` renders as just `"root"`, or the bar is blanked entirely — left as an implementation
choice, not a behavior contract).

### 3.6 Layout: shared `ContentContainer`

Rather than editing the `Y=1, Height=Dim.Fill()-1` boilerplate at all 6 `SwitchTo*` call sites,
`ViewManager` introduces one internal container view:

```
MainWindow (Window)
├─ MenuBar                (Y = 0)
├─ BreadcrumbBar           (Y = 1, Height = 1)
├─ ContentContainer (View) (Y = Pos.Bottom(breadcrumbBar), Height = Dim.Fill() - 1)
│   └─ <current content view>  (X=0, Y=0, Width=Dim.Fill(), Height=Dim.Fill())
└─ StatusBar               (Y = Pos.AnchorEnd(1))
```

`BreadcrumbBar` and `ContentContainer` are created once in `ViewManager`'s constructor and added
to `_container` (the `Window`) there. `SwapView` adds/removes the active content view into
`ContentContainer` instead of `_container`, and every `SwitchTo*` method drops its `X/Y/Width/
Height` assignment (now fixed and identical for all of them, set once on `ContentContainer`).
This is a net simplification, not just a breadcrumb accommodation.

Terminal.Gui `Adornments` (Padding/Border/Margin) were considered and rejected: they decorate a
single view's own bounds, not a sibling relationship ("bar above container"), so a container +
`Pos.Bottom` is the correct fit here.

### 3.7 Parent-level navigation (Tree modes only, in scope)

Clicking a breadcrumb segment (or a to-be-decided keyboard shortcut) while in a Tree mode moves
the tree's selection to that ancestor node and scrolls it into view. The ancestor node is already
materialized in memory (it's on the `ParentNode` chain of the currently selected node), so no
re-scan is needed — this is a pure selection/scroll operation.

**Open item for implementation:** the exact Terminal.Gui v2 API to select-and-scroll-to an
already-known node (e.g. `TreeView.GoTo(object)`, or manually setting `SelectedObject` plus a
scroll/`EnsureVisible` call) is not yet confirmed. To be verified during Step 1/2 implementation
rather than guessed here.

---

## 4. Testing

- `KeyPathBuilderTests.cs` (moved from `AppKeyHandlerTests.cs`, renamed type only).
- `KeyPathFormatterTests.cs` (new): empty path → `"root"`; key-only path; path containing an
  `Index` segment with `collapseIndices: true` vs `false`; segment-range output used for click
  mapping.
- `ViewManagerTests.cs`: extend to cover the Phase 1 vs Phase 2 `collapseIndices` distinction from
  3.4/3.5 directly — a `SingleDrillDownRequest` result with an `Index` segment in its `KeyPath`
  must render the literal index (`collapseIndices: false`), while a
  `FullAggregationDrillDownRequest` result with an `Index` segment must render `[*]`
  (`collapseIndices: true`). This is the regression test for Finding 1 of the design review.
- `BreadcrumbBarTests.cs` (new): `SetPath` updates displayed text; a simulated click within a
  segment's column range raises `SegmentActivated` with the correct index.
- `AppStateTests.cs`: extend for `CurrentKeyPath` default (`[]`) — likely a small addition to an
  existing test file rather than a new one.
- Extend existing `JsonLinesTreeViewTests` / `JsonArrayTreeViewTests` / `JsonObjectTreeViewTests`
  (and their `.Create` counterparts) to assert `onPathChanged` fires with the expected KeyPath on
  simulated selection change.

## 5. Files Touched

**New:**
- `src/App/KeyPathBuilder.cs`
- `src/App/KeyPathFormatter.cs`
- `src/App/Views/BreadcrumbBar.cs`
- `tests/DataMorph.Tests/App/KeyPathBuilderTests.cs`
- `tests/DataMorph.Tests/App/KeyPathFormatterTests.cs`
- `tests/DataMorph.Tests/App/Views/BreadcrumbBarTests.cs`

**Modified:**
- `src/App/AppState.cs` — add `CurrentKeyPath`
- `src/App/AppKeyHandler.cs` — remove `BuildKeyPath`, call `KeyPathBuilder.Build` instead
- `src/App/DrillDownRequest.cs` — add `KeyPath` to `SingleDrillDownRequest`
- `src/App/ViewManager.cs` — `ContentContainer` + `BreadcrumbBar` wiring, `UpdateBreadcrumb`,
  simplified `SwitchTo*` layout code
- `src/App/Views/MorphTreeView.cs` — `onSelectionChanged` constructor parameter
- `src/App/Views/RangeTreeViewBase.cs` — forward `onSelectionChanged` to `base(...)` so it
  reaches `MorphTreeView` from `JsonLinesTreeView`/`JsonArrayTreeView`
- `src/App/Views/JsonLinesTreeView.cs`, `JsonArrayTreeView.cs`, `JsonObjectTreeView.cs` —
  `onPathChanged` parameter threaded through `Create`
- `tests/DataMorph.Tests/App/AppKeyHandlerTests.cs` — remove moved `BuildKeyPath` tests
