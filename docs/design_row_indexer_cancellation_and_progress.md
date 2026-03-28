# RowIndexer Initial Display, Indexing Progress, and Cancellation — Design Document

## Scope

Three related problems are addressed together in this document because they share
the same code paths in `RowIndexer`, `FileLoader`, and `MainWindow`.

**Problem A — Empty initial display:** `FileLoader` constructs `JsonLinesTreeView`
before any rows have been indexed, so the tree is always empty on first render.

**Problem B — No progress feedback:** While `RowIndexer.BuildIndex` runs in the
background there is no visual indication of how much of the file has been
processed, producing a frozen-looking UI for large files.

**Problem C — Uncontrolled background task:** `BuildIndex` runs as a
fire-and-forget `Task.Run` with no `CancellationToken`. Opening a new file while
indexing is in progress, or quitting the application, leaves the previous
indexing task running to completion silently. This wastes CPU and I/O, and can
cause late-arriving `Application.Invoke` callbacks to race against a new load
cycle.

Solving all three together avoids multiple churn cycles on the same files and
produces a coherent, safe lifecycle for background indexing.

---

## 1. Requirements

### Functional Requirements

- `RowIndexer.BuildIndex` must accept a `CancellationToken` and exit
  cooperatively when cancellation is requested.
- `RowIndexer` must expose a `FirstCheckpointReached` event that fires exactly
  once when the first 1,000-row checkpoint is indexed.
- `RowIndexer` must expose `FileSize` and `BytesRead` properties so callers can
  compute a progress percentage without a lock.
- `RowIndexer` must raise `ProgressChanged` on every checkpoint boundary and
  `BuildIndexCompleted` when scanning ends (including on cancellation and error).
- `FileLoader.LoadJsonLinesAsync` must cancel any in-flight `BuildIndex` before
  starting a new one, and must await `FirstCheckpointReached` before setting
  `AppState.CurrentMode`.
- `FileLoader.Dispose` must cancel any in-flight `BuildIndex`.
- `MainWindow` must display a progress bar while `BuildIndex` is running and
  dismiss it when indexing completes or is cancelled.
- The progress display must show: percentage complete, bytes read, and total
  file size.
- After cancellation the progress bar must disappear cleanly with no stale
  callbacks updating the new file's UI.

### Non-Functional Requirements

- Cancellation must be cooperative: `BuildIndex` checks the token at each
  buffer-read iteration so it exits within one buffer read (~1 MB) of the
  cancel signal.
- All three events must fire from the indexing thread without blocking it.
- `BytesRead` must be readable from the UI thread without a lock.
- `BuildIndexCompleted` must fire even on cancellation and I/O error, so the
  progress bar is never orphaned.
- The empty-file path must not deadlock: `FirstCheckpointReached` fires even
  when no checkpoint is ever reached.
- Native AOT compatible; no reflection.

---

## 2. Design

### 2.1 Root Causes

**Empty display and no progress** share the same root: `FileLoader` fires
`Task.Run(indexer.BuildIndex)` without awaiting any readiness signal, and
`RowIndexer` exposes no progress surface.

**Uncontrolled cancellation** has a different root: `BuildIndex` takes no
`CancellationToken`, so `FileLoader` has no way to stop it. The existing
`AppState.Cts` is wired only to `IncrementalSchemaScanner`; `BuildIndex` has
always been truly fire-and-forget.

### 2.2 Lifecycle Model (After Fix)

```
FileLoader.LoadJsonLinesAsync(path)
  │
  ├─ CancelPreviousBuildIndexAsync()   ← cancel old CTS, await old task
  ├─ _buildIndexCts = new CTS
  ├─ new RowIndexer(path)
  ├─ subscribe FirstCheckpointReached → tcs.SetResult()
  ├─ subscribe ProgressChanged        → Application.Invoke(UpdateProgressBar)
  ├─ subscribe BuildIndexCompleted    → Application.Invoke(DismissProgressBar)
  ├─ _buildIndexTask = Task.Run(() => indexer.BuildIndex(_buildIndexCts.Token))
  ├─ ShowProgressBar()
  ├─ await tcs.Task                   ← unblocks when ≥1 000 rows ready
  └─ update AppState, switch view
```

On new file open or `Dispose`:

```
CancelPreviousBuildIndexAsync()
  ├─ _buildIndexCts?.Cancel()
  └─ await _buildIndexTask (silently swallows OperationCanceledException)
```

### 2.3 `RowIndexer.cs` — Changes

#### New Signature for `BuildIndex`

```csharp
/// <summary>
/// Builds the row index by scanning the entire file.
/// Exits cooperatively when <paramref name="ct"/> is cancelled.
/// NOT thread-safe — call once from a single background thread.
/// <see cref="BuildIndexCompleted"/> fires unconditionally when this method
/// returns, regardless of whether it completed, was cancelled, or threw.
/// </summary>
public void BuildIndex(CancellationToken ct = default)
```

The default value keeps all existing call sites that pass no token unchanged
(the CSV indexer, tests, and the CLI pipeline are unaffected).

#### New Properties

```csharp
/// <summary>Total file size in bytes. Set once before scanning begins.</summary>
public long FileSize { get; private set; }

/// <summary>
/// Bytes read so far. Updated atomically after each buffer read.
/// Safe to read from any thread.
/// </summary>
public long BytesRead => Interlocked.Read(ref _bytesRead);

private long _bytesRead;
```

#### New Events

```csharp
/// <summary>
/// Raised once when the first checkpoint (CheckPointInterval rows) has been
/// indexed. Fired from the indexing thread; subscribers must not block.
/// </summary>
public event Action? FirstCheckpointReached;

/// <summary>
/// Raised on every checkpoint boundary.
/// Arguments: (bytesRead, fileSize).
/// Fired from the indexing thread; subscribers must not block.
/// </summary>
public event Action<long, long>? ProgressChanged;

/// <summary>
/// Raised once when BuildIndex returns — whether it completed normally,
/// was cancelled, or threw an exception.
/// Fired from the indexing thread (inside the finally block).
/// </summary>
public event Action? BuildIndexCompleted;

private bool _firstCheckpointReached;
```

#### Updated `BuildIndex` Body

```csharp
public void BuildIndex(CancellationToken ct = default)
{
    FileSize = new FileInfo(_filePath).Length;

    using var handle = File.OpenHandle(
        _filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
    var scanner = new RowScanner();

    try
    {
        var fileOffset = 0L;
        var rowCount = 0L;
        var lastByteRead = (byte)0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();   // ← cooperative cancellation

            var bytesRead = RandomAccess.Read(
                handle, buffer.AsSpan(0, BufferSize), fileOffset);
            if (bytesRead <= 0)
                break;

            Interlocked.Add(ref _bytesRead, bytesRead);
            lastByteRead = buffer[bytesRead - 1];

            ProcessBuffer(buffer.AsSpan(0, bytesRead),
                ref fileOffset, ref rowCount, ref scanner);
            fileOffset += bytesRead;
        }

        // Empty-file / sub-checkpoint guard: FirstCheckpointReached must
        // always fire so that the TaskCompletionSource in FileLoader does
        // not hang.
        if (!_firstCheckpointReached)
        {
            _firstCheckpointReached = true;
            FirstCheckpointReached?.Invoke();
        }

        if (Interlocked.Read(ref _bytesRead) > 0 && lastByteRead != (byte)'\n')
            rowCount++;

        Interlocked.Exchange(ref _totalRows, rowCount);
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
        BuildIndexCompleted?.Invoke();   // always fires
    }
}
```

Cancellation propagates as `OperationCanceledException` out of the `Task.Run`
wrapper. `BuildIndexCompleted` still fires in the `finally` block so the UI can
always dismiss the progress bar.

When `BuildIndex` is cancelled before the first checkpoint, the
empty-file/sub-checkpoint guard in the `try` block does **not** run (the
exception unwinds past it). To guarantee `FirstCheckpointReached` fires on
cancellation as well — so `tcs.Task` never hangs — the guard is duplicated in
the `finally` block:

```csharp
finally
{
    // Guarantee FirstCheckpointReached fires even on cancellation or error,
    // so the TaskCompletionSource in FileLoader never hangs.
    if (!_firstCheckpointReached)
    {
        _firstCheckpointReached = true;
        FirstCheckpointReached?.Invoke();
    }

    ArrayPool<byte>.Shared.Return(buffer);
    BuildIndexCompleted?.Invoke();
}
```

#### Updated `ProcessBuffer` — Checkpoint Block

```csharp
if (rowCount % CheckPointInterval == 0)
{
    Interlocked.Exchange(ref _totalRows, rowCount);

    var checkpointOffset = fileOffset + position;
    lock (_lock)
    {
        _checkpoints.Add(checkpointOffset);
    }

    if (!_firstCheckpointReached)
    {
        _firstCheckpointReached = true;
        FirstCheckpointReached?.Invoke();
    }

    ProgressChanged?.Invoke(Interlocked.Read(ref _bytesRead), FileSize);
}
```

### 2.4 `FileLoader.cs` — Cancellation + Await

`FileLoader` owns a `CancellationTokenSource` and `Task` for the current
`BuildIndex` operation. Both are replaced on every `LoadJsonLinesAsync` call.

```csharp
private CancellationTokenSource? _buildIndexCts;
private Task _buildIndexTask = Task.CompletedTask;

private async Task CancelPreviousBuildIndexAsync()
{
    _buildIndexCts?.Cancel();
    try
    {
        await _buildIndexTask.ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // Expected; swallow.
    }
}

private async Task<Result> LoadJsonLinesAsync(string filePath)
{
    await CancelPreviousBuildIndexAsync();

    _buildIndexCts?.Dispose();
    _buildIndexCts = new CancellationTokenSource();
    var ct = _buildIndexCts.Token;

    RowIndexer indexer;
    try
    {
        indexer = new RowIndexer(filePath);
    }
    catch (Exception ex) when (ex is IOException or ArgumentException)
    {
        return Results.Failure(ex.Message);
    }

    var tcs = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);

    indexer.FirstCheckpointReached += () => tcs.SetResult();

    _buildIndexTask = Task.Run(() => indexer.BuildIndex(ct));

    // Wait until at least 1,000 rows are indexed (or file is empty/cancelled).
    await tcs.Task.ConfigureAwait(false);

    // If cancelled before the view is set up, abort silently.
    if (ct.IsCancellationRequested)
        return Results.Failure("Load cancelled.");

    _state.JsonLinesIndexer = indexer;
    _state.JsonLinesSchemaScanner = null;
    _state.Schema = null;
    _state.OnSchemaRefined = null;
    _state.CurrentMode = ViewMode.JsonLinesTree;

    return Results.Success();
}

public void Dispose()
{
    if (_disposed)
        return;

    _buildIndexCts?.Cancel();
    _buildIndexCts?.Dispose();
    _disposed = true;
}
```

`FileLoader.Dispose` is already called from `MainWindow.Dispose`, so quitting
the application cancels any in-flight `BuildIndex` automatically.

### 2.5 `JsonLinesTreeView.cs` — Remove Async Wait

Because `FileLoader` guarantees data availability before constructing the view,
`LoadInitialRootNodes` becomes synchronous:

```csharp
public JsonLinesTreeView(RowIndexer indexer, Action onTableModeToggle)
{
    _cache = new RowByteCache(indexer);
    _onTableModeToggle = onTableModeToggle;
    LoadInitialRootNodes();
    ObjectActivated += OnObjectActivated;
}

private void LoadInitialRootNodes()
{
    var linesToLoad = Math.Min(_cache.TotalLines, InitialLoadCount);
    for (var i = 0; i < linesToLoad; i++)
    {
        var lineBytes = _cache.GetLineBytes(i);
        if (lineBytes.IsEmpty)
            continue;
        AddObject(CreateRootNode(lineBytes, i));
    }
}
```

### 2.6 Progress Bar in `MainWindow`

Subscriptions are registered immediately after `FileLoader.LoadAsync` returns
successfully. They are scoped to the indexer instance, so stale callbacks from
a cancelled indexer never reach the new file's UI: `DismissProgressBar` is
called via `Application.Invoke`, but the dismissed bar belongs to the previous
load cycle and the new bar is shown for the new indexer.

```csharp
// In MainWindow, after SwitchToJsonLinesTree:
if (_state.JsonLinesIndexer is { } indexer)
{
    ShowIndexingProgress();

    indexer.ProgressChanged += (bytesRead, fileSize) =>
        Application.Invoke(() => UpdateIndexingProgress(bytesRead, fileSize));

    indexer.BuildIndexCompleted +=
        () => Application.Invoke(DismissIndexingProgress);
}
```

`UpdateIndexingProgress` updates `ProgressBar.Fraction` and a companion label:

```csharp
private void UpdateIndexingProgress(long bytesRead, long fileSize)
{
    if (fileSize <= 0)
        return;
    var fraction = (float)bytesRead / fileSize;
    _progressBar.Fraction = fraction;
    _progressLabel.Text =
        $"Indexing… {fraction * 100:F0}%  " +
        $"({FormatBytes(bytesRead)} / {FormatBytes(fileSize)})";
}
```

`FormatBytes` formats raw byte counts as human-readable strings (KB, MB, GB).
`DismissIndexingProgress` removes the bar and label from the layout.

---

## 3. Files Changed

| File | Change |
|---|---|
| `src/Engine/IO/JsonLines/RowIndexer.cs` | Add `CancellationToken` to `BuildIndex`; add `FileSize`, `BytesRead`, three events; update `ProcessBuffer` |
| `src/App/IO/FileLoader.cs` | Add `_buildIndexCts` / `_buildIndexTask`; cancel previous task on new load; await `FirstCheckpointReached` |
| `src/App/Views/JsonLinesTreeView.cs` | Make `LoadInitialRootNodes` synchronous |
| `src/App/MainWindow.cs` | Subscribe to `ProgressChanged` / `BuildIndexCompleted`; show/dismiss progress bar |

---

## 4. Test Plan

### `RowIndexerTests` (extend existing file)

| Test method | Scenario |
|---|---|
| `BuildIndex_RaisesFirstCheckpointReached_OnFirstThousandRows` | Event fires after 1,000 rows |
| `BuildIndex_FirstCheckpointReached_FiresOnlyOnce` | Fires exactly once for a 3,000-row file |
| `BuildIndex_RaisesFirstCheckpointReached_WhenFileIsEmpty` | Fires for a zero-byte file |
| `BuildIndex_RaisesFirstCheckpointReached_WhenCancelledBeforeFirstCheckpoint` | Fires even when cancelled before 1,000 rows |
| `BuildIndex_RaisesProgressChanged_OnEachCheckpoint` | Fires N times for N × 1,000 rows |
| `BuildIndex_RaisesBuildIndexCompleted_AfterCompletion` | Fires after full scan |
| `BuildIndex_RaisesBuildIndexCompleted_WhenCancelled` | Fires even when cancelled mid-scan |
| `BuildIndex_RaisesBuildIndexCompleted_WhenFileIsEmpty` | Fires for a zero-byte file |
| `BuildIndex_WhenCancelled_ThrowsOperationCanceledException` | Task wrapping `BuildIndex` faults with OCE |
| `BytesRead_IncreasesMonotonically_DuringBuildIndex` | Observed from a concurrent reader thread |
| `FileSize_MatchesActualFileLength` | `FileSize == new FileInfo(path).Length` |

### `FileLoaderTests` (extend existing file)

| Test method | Scenario |
|---|---|
| `LoadAsync_WhenCalledTwice_CancelsPreviousBuildIndex` | Second call cancels first; no hang |
| `Dispose_CancelsBuildIndex` | `Dispose` while indexing; task completes without hang |

Progress-bar UI wiring is verified by manual testing.

---

## Decision Record

### Rationale

**`CancellationToken` as a default parameter on `BuildIndex`** allows the
existing call sites (`DataRowIndexer`, CLI pipeline, all tests) to remain
unchanged. The token is only meaningful for the TUI path where a background
scan can be interrupted.

**`CancelPreviousBuildIndexAsync` in `FileLoader`** is the single
authoritative cancellation point. Placing it in `FileLoader` keeps
`RowIndexer` unaware of the UI lifecycle, preserving the Engine/App separation
already established in the codebase.

**`FirstCheckpointReached` in the `finally` block** (not only in the `try`
block) guarantees the `TaskCompletionSource` resolves even when `BuildIndex`
is cancelled before the first checkpoint. Without this guard, a fast cancel
(e.g., the user opens a second file immediately) would leave `tcs.Task`
pending forever, blocking `LoadJsonLinesAsync`.

**`BuildIndexCompleted` in the `finally` block** ensures the progress bar is
always dismissed, including on cancellation and I/O error. A stale progress
bar would be confusing and would persist across subsequent file loads.

**Event subscriptions are scoped to the indexer instance.** When `FileLoader`
cancels an old indexer and creates a new one, the old indexer's events still
fire (in the `finally` block) and call `Application.Invoke(DismissProgressBar)`.
This is safe: `DismissProgressBar` is idempotent and the new indexer's bar is
shown separately. No explicit unsubscription is needed.

**`TaskCreationOptions.RunContinuationsAsynchronously`** on the
`TaskCompletionSource` ensures the continuation that resumes `LoadJsonLinesAsync`
runs on the thread pool rather than inline on the indexing thread, preventing
the indexing thread from stalling on UI work.

### Alternatives Considered

**A — Storing the `CancellationTokenSource` in `AppState`**

`AppState.Cts` already exists for `IncrementalSchemaScanner`. Reusing it for
`BuildIndex` would couple the two cancellation scopes: cancelling the schema
scanner would also cancel indexing, and vice versa. Keeping a dedicated
`_buildIndexCts` in `FileLoader` keeps the two background operations
independently controllable.

**B — `IDisposable` on `RowIndexer` to signal cancellation**

Rejected. `IDisposable` conventionally releases resources; using it as a
cancellation mechanism would be surprising to readers. A `CancellationToken`
parameter is the idiomatic .NET pattern for cooperative cancellation of a
long-running method.

**C — Polling `BytesRead` on a UI-thread timer**

Rejected. A timer fires on a fixed interval regardless of indexing progress,
wasting CPU when the disk is the bottleneck. The event-driven approach fires
only when a meaningful boundary is crossed.

**D — Separate PRs for each problem**

Considered. The three problems touch the same `BuildIndex` loop, the same
`FileLoader.LoadJsonLinesAsync`, and the same `MainWindow` progress area.
Splitting them would require coordinating merge order to avoid conflicts and
would leave intermediate states (cancellation added but no progress bar, or
progress bar but no cancellation) that are harder to test end-to-end than a
single coherent change.

### Consequences

- `BuildIndex` now has a `CancellationToken` parameter. The existing default
  value (`= default`) means all current callers compile without change.
- `JsonLinesTreeView` can no longer be constructed before `FileLoader` has
  completed `await tcs.Task`. This is a strengthened precondition, not a
  regression.
- Files with fewer than 1,000 rows use the `finally`-block guard path: all
  events fire at end-of-file. The tree populates correctly because `TotalRows`
  is finalised before `FirstCheckpointReached` fires in that path.
- `RowIndexer` carries two additional `long` fields and three `event` delegate
  fields (~50 bytes per instance); overhead is negligible.
- The progress bar is currently wired in `MainWindow`. If `ViewManager` later
  takes over full view lifecycle responsibility, the progress bar wiring should
  migrate to `ViewManager.SwitchToJsonLinesTree`.
- `DataRowIndexer` (CSV) is **not** changed by this document. If the same
  progress and cancellation improvements are desired for CSV files, a follow-up
  design document should address that separately.

---

## 5. Implementation Steps

### Step 1 — `RowIndexer`: Add properties and events (declarations only)

Add the following to `RowIndexer.cs`:

- `public long FileSize { get; private set; }`
- `private long _bytesRead;`
- `public long BytesRead => Interlocked.Read(ref _bytesRead);`
- `public event Action? FirstCheckpointReached;`
- `public event Action<long, long>? ProgressChanged;`
- `public event Action? BuildIndexCompleted;`
- `private bool _firstCheckpointReached;`

No logic changes yet. Build must pass.

---

### Step 2 — `RowIndexer`: Update `BuildIndex` signature and body

1. Add `CancellationToken ct = default` parameter to `BuildIndex`.
2. Set `FileSize = new FileInfo(_filePath).Length;` at the start.
3. Add `ct.ThrowIfCancellationRequested();` at the top of the read loop.
4. Add `Interlocked.Add(ref _bytesRead, bytesRead);` after each `RandomAccess.Read`.
5. Move the `ArrayPool<byte>.Shared.Return(buffer)` call into a `finally` block.
6. Add the `FirstCheckpointReached` guard to the `finally` block (fires if not yet fired).
7. Add `BuildIndexCompleted?.Invoke();` at the end of the `finally` block.

Build must pass.

---

### Step 3 — `RowIndexer`: Update `ProcessBuffer` checkpoint block

Inside the existing `if (rowCount % CheckPointInterval == 0)` block:

1. Add the `FirstCheckpointReached` guard (fires once on first checkpoint).
2. Add `ProgressChanged?.Invoke(Interlocked.Read(ref _bytesRead), FileSize);`.

Build must pass.

---

### Step 4 — `RowIndexerTests`: Add new test methods

Extend the existing test file with all 11 test methods listed in Section 4.
Bodies are `// Arrange / // Act / // Assert` stubs only.

Build must pass.

---

### Step 5 — `FileLoader`: Add cancellation fields and `CancelPreviousBuildIndexAsync`

1. Add `private CancellationTokenSource? _buildIndexCts;` field.
2. Add `private Task _buildIndexTask = Task.CompletedTask;` field.
3. Add `private async Task CancelPreviousBuildIndexAsync()` method (body: cancel + await + swallow OCE).
4. Update `Dispose` to call `_buildIndexCts?.Cancel()` and `_buildIndexCts?.Dispose()`.

Build must pass.

---

### Step 6 — `FileLoader`: Update `LoadJsonLinesAsync`

1. Call `await CancelPreviousBuildIndexAsync()` at the top.
2. Dispose and recreate `_buildIndexCts`.
3. Create `TaskCompletionSource` with `RunContinuationsAsynchronously`.
4. Subscribe `indexer.FirstCheckpointReached += () => tcs.SetResult()`.
5. Assign `_buildIndexTask = Task.Run(() => indexer.BuildIndex(ct))`.
6. `await tcs.Task`.
7. Guard: if `ct.IsCancellationRequested`, return failure.
8. Move the `AppState` updates to after the await.

Build must pass.

---

### Step 7 — `FileLoaderTests`: Add new test methods

Extend the existing test file with the 2 test methods listed in Section 4.
Bodies are `// Arrange / // Act / // Assert` stubs only.

Build must pass.

---

### Step 8 — `JsonLinesTreeView`: Make `LoadInitialRootNodes` synchronous

1. Replace `_ = LoadInitialRootNodesAsync()` with `LoadInitialRootNodes()` in the constructor.
2. Remove `async` and `await Task.Delay(100)` from `LoadInitialRootNodes`.
3. Rename the method to `LoadInitialRootNodes`.

Build must pass.

---

### Step 9 — `MainWindow`: Add progress bar UI and wiring

1. Add `_progressBar` (`ProgressBar`) and `_progressLabel` (`Label`) fields.
2. Implement `ShowIndexingProgress()` — adds bar and label to the layout.
3. Implement `UpdateIndexingProgress(long bytesRead, long fileSize)` — updates fraction and label text.
4. Implement `DismissIndexingProgress()` — removes bar and label from the layout (idempotent).
5. Implement `FormatBytes(long bytes)` — formats as KB / MB / GB string.
6. After `SwitchToJsonLinesTree`, wire up `ProgressChanged` and `BuildIndexCompleted` on the new indexer, then call `ShowIndexingProgress()`.
7. Initialize the progress bar with the current `BytesRead / FileSize` immediately after subscribing (to account for the already-fired first `ProgressChanged`).

Build must pass.

---

### Step 10 — Implement all test bodies

Fill in the `// Arrange / // Act / // Assert` stubs for all 13 test methods
(11 in `RowIndexerTests`, 2 in `FileLoaderTests`).

Run `dotnet test` — all tests must pass.
