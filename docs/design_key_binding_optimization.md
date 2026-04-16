# Key Binding Optimization Design Document

## Overview

This design document outlines the optimization of key bindings for MorphActions and file operations in DataMorph, focusing on **ergonomics**, **discoverability** through a context-sensitive menu (`x`), and **efficiency** through single-key shortcuts.

**Research Sources:** This design is inspired by modern TUI tools like `lazydocker`, `k9s`, and `ranger`.

## Requirements

### 1. Context-Sensitive Action Menu (`x`)
- Primary method for discovery: `x` opens a menu of all available actions for the current selection.
- Ergonomic placement: Left-hand `x` complements right-hand `hjkl` navigation.

### 2. Single-Key File Operations
- Replace `Ctrl+letter` with single-key mnemonics for common file operations.
- `o` for Open, `s` for Save, `q` for Quit.
- This streamlines the interface by removing the need for modifiers during frequent tasks.

### 3. Contextual Help
- `?` for a full key-binding overlay.

## Architectural Decision Records (ADR)

### ADR 1: Adoption of `x` for Context-Sensitive Action Menu

**Status:** Accepted  
**Context:** MorphActions in DataMorph require choosing an operation for the current column. Previous `Shift+letter` combinations were non-discoverable and ergonomically taxing.  
**Decision:** Adopt `x` as the primary trigger for a context-sensitive action menu, following the pattern established by modern TUI tools like `lazydocker`.  
**Rationale:** 
1. **Ergonomics:** `x` is located on the left hand's home row area, allowing the right hand to stay on `hjkl` for navigation. This "right-hand navigates, left-hand acts" split improves operational tempo, as seen in `lazydocker`.
2. **Discoverability:** Unlike a command palette which requires *recalling* a command name, a menu allows *recognizing* options. This lowers the barrier for new users.
3. **Convention:** `x` has become a de-facto symbol for "extra actions" or "context menu" in the TUI community, similar to a right-click in GUI.
4. **Namespace Preservation:** Reserved `m` for potential future "Mark" or "Modify" features and `t` for switching between Tree and Table views.


### ADR 2: Single-Key File Operations (`o`, `s`, `q`)


**Status:** Accepted  
**Context:** Common operations like Open, Save, and Quit were bound to `Ctrl+O/S/X`.  
**Decision:** Introduce single-key shortcuts `o`, `s`, and `q` as the primary methods for these operations.  
**Rationale:** 
1. **Efficiency:** DataMorph is an interactive tool where users frequently open and close files. Removing the `Ctrl` modifier reduces physical strain.
2. **Consistency:** Many high-performance TUI tools (e.g., `htop`, `btop`, `lazygit`) use `q` for quit.
3. **Safety:** While single-key actions can be accidental, `q` and `s` will trigger confirmation dialogs when unsaved changes exist (Recipes), mitigating risk.

---

## Proposed Key Bindings

### 1. Global / File Operations
These keys work globally, provided no input field or modal is focused.

| Key | Function | Change | Rationale |
|-----|----------|--------|-----------|
| **o** | Open File | **NEW** | More efficient than Ctrl+O |
| **s** | Save Recipe | **NEW** | More efficient than Ctrl+S |
| **q** | Quit | **NEW** | Industry standard for TUIs (lazygit, htop) |
| **t** | Tree/Table View | Keep | Switch between hierarchical (Tree) and flattened (Table) view |
| **?** | Help | NEW | Contextual help overlay |
### 2. Navigation
Standard Vim-like navigation.

| Key | Function |
|-----|----------|
| **h/j/k/l** | Move Left/Down/Up/Right |
| **gg** | Jump to first row |
| **G** (Shift+g) | Jump to last row |

### 3. MorphActions (Current Item Context)
Actions apply to the currently focused column or row.

| Key | Function |
|-----|----------|
| **x** | **Action Menu** (All available actions) |

---

## UI Components Design

### 1. The Action Menu (`x`)
When the user presses `x`, a modal list appears at the current cursor position or centered.
- **Content**: A list of actions valid for the current focus (e.g., Rename, Cast, Delete).
- **Navigation**: Users navigate the list using **j/k** or **Up/Down arrow keys**.
- **Execution**: Pressing **Enter** executes the highlighted action.
- **Cancellation**: Pressing **Esc** or `x` again closes the menu.

### 2. Status Bar Hints
The status bar dynamically updates to show the most relevant keys for the current state.

- **Default**: `o:Open  s:Save  q:Quit  t:Tree/Table  x:Menu  ?:Help`

---

## Future Considerations

1.  **In-Menu Shortcuts**: If there is high user demand for efficiency, we may allow triggering actions within the `x` menu by pressing their mnemonic key (e.g., `x` then `r` for Rename).

