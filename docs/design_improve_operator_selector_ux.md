# Design: Improve Operator Selector UX in Filter Column Dialog

## Requirements

The operator selector in the Filter Column dialog must behave like a standard radio group:
arrow key navigation should immediately select the focused item. Currently,
`OptionSelector<FilterOperator>` only moves highlight on arrow keys and requires an explicit
Space or Enter keypress to confirm the selection, which is unintuitive.

## Root Cause

`OptionSelector<FilterOperator>` (from Terminal.Gui v2) uses `CheckBox` subviews with radio
style. Its `MoveNext`/`MovePrevious` methods (bound to the arrow keys) only call `SetFocus()`
on the target checkbox — they do not update `Value`. `Value` changes only when a checkbox is
*activated* (Space/Enter or double-click). This is the correct behavior for a multi-step
selector, but it does not match the intuitive "select-on-navigate" radio button UX.

## Approach

Introduce `AutoSelectOptionSelector<TEnum>` — a thin subclass of `OptionSelector<TEnum>` that
overrides `CreateSubViews()`. After calling the base implementation, it subscribes to each
`CheckBox` child's `HasFocusChanged` event. When a checkbox gains focus, the selector's
`SelectorBase.Value` is immediately updated to that checkbox's integer value, which in turn
causes `OptionSelector<TEnum>.Value` (the typed `TEnum?` property) to reflect the newly
focused item without requiring any additional keypress.

Replace `OptionSelector<FilterOperator>` with `AutoSelectOptionSelector<FilterOperator>` in
`FilterColumnDialog`.

## Files to Change

| File | Change |
|------|--------|
| `src/App/Views/AutoSelectOptionSelector.cs` | **New** — `AutoSelectOptionSelector<TEnum>` class |
| `src/App/Views/Dialogs/FilterColumnDialog.cs` | Replace `OptionSelector<FilterOperator>` with `AutoSelectOptionSelector<FilterOperator>` |

## Implementation Notes

- `AutoSelectOptionSelector<TEnum>` subscribes to each `CheckBox`'s `HasFocusChanged` event
  inside `CreateSubViews()`, which is called once at construction time (triggered by
  `OptionSelector<TEnum>()`'s `Labels = Enum.GetValues<TEnum>()...` assignment).
- To update `Value`, the handler casts `this` to `SelectorBase` and sets the `int?` property
  directly. This bypasses `OptionSelector<TEnum>`'s hiding `new` declaration and invokes the
  real `SelectorBase.Value` setter, which handles validation, `UpdateChecked()`, and events.
- No changes are needed to `FilterOperator`, `FilterAction`, `FilterEvaluator`, or any
  callers of `FilterColumnDialog` — the public API of `FilterColumnDialog` is unchanged.
- `App/Views` components are not unit-tested in this project (the testing guidelines
  prioritize engine hot-path logic). No new test file is required.

## Decision Record

### Rationale

Subscribing to `HasFocusChanged` on each child `CheckBox` is the narrowest correct hook:
focus changes whenever the user presses an arrow key (via `SelectorBase`'s `MoveNext` /
`MovePrevious` commands), so wiring `Value` to focus gives exactly the desired behavior
without duplicating any key-binding or layout logic.

### Alternatives Considered

1. **Override `MoveNext`/`MovePrevious` in a subclass** — These methods are `private` in
   `SelectorBase`, making them inaccessible to subclasses. Forking the methods would require
   copying a significant amount of internal logic and would break on future Terminal.Gui
   updates.

2. **Handle `KeyDown` on `FilterColumnDialog`** — Listening for `CursorDown`/`CursorUp` at
   the dialog level and manually updating `selector.Value` is fragile: it would need to
   replicate the orientation-aware focus-traversal logic already in `SelectorBase`, and could
   interfere with other key handlers in the dialog.

3. **Replace with `DropDownList`** — A dropdown changes the interaction model (click to open,
   then select). It does not match the "always-visible radio button list" UX described in the
   issue.

4. **Replace with `ListView`** — A `ListView` would need custom rendering, value mapping, and
   keyboard handling to replace what `OptionSelector<TEnum>` already provides. Higher
   complexity for the same outcome.

### Consequences

- `AutoSelectOptionSelector<TEnum>` is a reusable generic component. If the same UX issue is
  later reported for `CastColumnDialog` (which also uses `OptionSelector<ColumnType>`), it can
  be swapped in without any design work.
- The change does not affect `CastColumnDialog` intentionally: that dialog's confirmation
  flow is different (it checks whether the selected type differs from the current type), and
  the issue specifically targets `FilterColumnDialog`.
- The `HasFocusChanged` subscription captures `int value` by value in a closure, which is
  safe and allocation-free after construction (no heap allocations on each keystroke).
