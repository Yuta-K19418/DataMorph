---
name: terminal-gui-v2
description: >
  Use this skill when implementing any feature using Terminal.Gui v2.
  Provides key guidelines and reference links so agents can make correct
  implementation decisions without spending time on research.

  Trigger whenever the user mentions:
  - Implementing or modifying a TUI view, dialog, table, tree, menu, or key binding
  - Any Terminal.Gui class: Window, View, Dialog, TableView, TreeView, StatusBar, MenuBar, etc.
  - Questions about Terminal.Gui v2 APIs, layout, input handling, or application lifecycle
  - "how do I implement X in Terminal.Gui" or "which class should I use for Y"
---

# Terminal.Gui v2 — Implementation Reference

## Reference Sources

Always look up the official docs and upstream source before implementing.

**Official API docs:**
`https://gui-cs.github.io/Terminal.Gui/api/Terminal.Gui.<Namespace>.html`

Key namespaces:
- `App` — `Application`, `IApplication`
- `ViewBase` — `View`, `Pos`, `Dim`
- `Views` — `Window`, `Dialog`, `TableView`, `TreeView`, `MenuBar`, `StatusBar`, etc.
- `Input` — `Key`, `KeyCode`, `Command`, `KeyBinding`

**Upstream source (v2_develop branch):**
```bash
# List files in a directory
gh api "repos/gui-cs/Terminal.Gui/contents/Terminal.Gui/<Dir>?ref=v2_develop" \
  | python3 -c "import json,sys; [print(d['name']) for d in json.load(sys.stdin)]"

# Read a specific file
gh api "repos/gui-cs/Terminal.Gui/contents/Terminal.Gui/<Dir>/<File>.cs?ref=v2_develop" \
  | python3 -c "import json,sys,base64; print(base64.b64decode(json.load(sys.stdin)['content']).decode())"
```

---

## Always Use v2 APIs

This project uses Terminal.Gui v2. **Do not use v1 APIs.** When in doubt about whether
an API is v1 or v2, verify against the official docs or upstream source listed above.

---

## Critical: Static `Application` Class is Deprecated

**DO NOT USE** — all static members below are marked `[Obsolete("The legacy static Application object is going away.")]`:

```
Application.Init()         Application.Shutdown()      Application.Instance
Application.Run(...)       Application.RequestStop()   Application.Invoke(...)
Application.AddTimeout()   Application.Begin()         Application.End()
```

**The only non-deprecated static method is `Application.Create()`.**

Always use the `IApplication` instance it returns:

```csharp
using var app = Application.Create().Init();
app.Run(mainWindow);
// app.Dispose() called automatically
```

Inside any `View` subclass, access the instance via the inherited `App` property:
```csharp
App.Run(dialog);
App.RequestStop();
App.Invoke(() => { ... });
```

---

## Disposal Rules

- Child views added via `Add()` are owned and disposed by the parent — do not dispose them manually.
- Views removed with `Remove()` are no longer owned — caller must dispose.
- `Dialog` instances run with `App.Run(dialog)` must be disposed by the caller (`using var`).
- Override `Dispose(bool disposing)` when the view owns non-view resources.
