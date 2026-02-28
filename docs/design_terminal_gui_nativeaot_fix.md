# Design: Fix Terminal.Gui NativeAOT Incompatibility

## Problem Statement

`dotnet publish -c Release -r osx-arm64` succeeds, but the generated NativeAOT binary exits
immediately without output (SIGKILL on macOS, unhandled exception on Linux).

## Root Cause Analysis

### The Crash

Confirmed by running the linux-arm64 NativeAOT binary in Docker:

```
Unhandled exception. System.InvalidOperationException: Theme is not a ConfigProperty.
   at Terminal.Gui.Configuration.Scope`1.GetUninitializedProperty(String)
   at Terminal.Gui.Configuration.SettingsScope..ctor()
   at Terminal.Gui.Configuration.ConfigurationManager.LoadHardCodedDefaults()
   at Terminal.Gui.Configuration.ConfigurationManager.Initialize()
   at Internal.Runtime.CompilerHelpers.StartupCodeHelpers.RunModuleInitializers()
```

### Cause

Terminal.Gui's `ConfigurationManager` uses `System.Reflection` to discover all properties
annotated with `[ConfigProperty]` (e.g., `SettingsScope.Theme`).

This discovery occurs in a **module initializer** (`[ModuleInitializer]`), which NativeAOT
runs **before `Main()`** as part of `RunModuleInitializers()`.

NativeAOT's IL trimmer removes property metadata that isn't directly referenced by
application code. When `SettingsScope.Theme` is trimmed, `typeof(T).GetProperty("Theme")`
returns `null`, causing Terminal.Gui to throw `InvalidOperationException`.

On macOS, the unhandled exception in a module initializer results in SIGKILL with no output.
On Linux, the exception text is written to stderr before termination.

### Why Warnings Were Suppressed

`DataMorph.App.csproj` currently suppresses `IL2026`, `IL3050`, `IL3053` — the exact
IL trimmer warnings that would have flagged this issue. This suppression masked the root
cause during development.

## Proposed Fix

### Approach: TrimmerRootDescriptor

Add an IL linker descriptor file that instructs the trimmer to preserve all types and
members from the `Terminal.Gui` assembly.

**Why this approach:**
- Terminal.Gui v2 (`2.0.0-develop.5039`) does not provide NativeAOT/trimmer-safe APIs
- No newer version with NativeAOT support is available
- We do not control Terminal.Gui's source code
- A `TrimmerRootDescriptor` is the standard .NET mechanism for preserving third-party
  reflection-dependent assemblies

**Trade-offs:**
- Binary size increases (Terminal.Gui metadata is preserved instead of trimmed)
- All other first-party code (`DataMorph.Engine`, `DataMorph.App`) remains fully trimmed
- No code changes required in application logic

**Alternatives rejected:**
| Approach | Reason Rejected |
|---|---|
| Disable NativeAOT | Loses core performance benefit; AOT is a project requirement |
| Per-type trimmer roots | Fragile; any new reflection in Terminal.Gui would silently re-break |
| Fork/patch Terminal.Gui | Out of scope; upstream project responsibility |
| Upgrade Terminal.Gui | No newer version with NativeAOT fixes available |

## Files to Change

### New: `src/App/TrimmerRoots.xml`

```xml
<linker>
  <!--
    Terminal.Gui uses System.Reflection in module initializers to discover
    [ConfigProperty]-annotated members. Preserve the entire assembly to prevent
    NativeAOT trimming from removing required metadata.
  -->
  <assembly fullname="Terminal.Gui" preserve="all" />
</linker>
```

### Modified: `src/App/DataMorph.App.csproj`

Add to an existing `<ItemGroup>`:

```xml
<TrimmerRootDescriptor Include="TrimmerRoots.xml" />
```

## Validation Plan

1. `dotnet format` — verify no style violations
2. `dotnet build` — verify zero warnings (TreatWarningsAsErrors is enabled)
3. Linux Docker run:
   ```sh
   docker run --rm -v "$(pwd):/src" -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
     bash -c "apt-get update -q && apt-get install -y clang zlib1g-dev -q && \
     dotnet publish src/App/DataMorph.App.csproj -c Release -r linux-arm64 -o /src/tmp-linux-publish"
   docker run --rm -it -v "$(pwd)/tmp-linux-publish:/app" \
     mcr.microsoft.com/dotnet/runtime-deps:10.0 /app/DataMorph.App
   ```
   Expected: Application starts (TUI renders) instead of crashing
4. macOS run: `dotnet publish -c Release -r osx-arm64` and run the binary
