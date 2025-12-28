# DataMorph Implementation Tasks

## Phase 1: Project Scaffolding (Infrastructure)
> Ref: docs/development_guidelines.md
- [ ] Create .NET 10 Solution and Projects (`App`, `Engine`, `Tests`).
- [ ] Configure strict `.csproj` settings (Zero-warning, Native AOT, AnalysisLevel).
- [ ] Set up GitHub Actions for CI (including AOT compilation check).
- [ ] Implement the `Result<T>` and `Result` types in `Engine` for zero-allocation error handling.

## Phase 2: Engine - Zero-Allocation Input Layer
> Ref: docs/design.md (Section 2.1)
- [ ] Implement `MmapService` for efficient file memory mapping.
- [ ] Implement `RowIndexer` using SIMD-accelerated newline detection.
- [ ] Create unit tests for `RowIndexer` using xUnit and BenchmarkDotNet.
- [ ] Implement `PipelinesEngine` for streaming data processing.

## Phase 3: Engine - High-Performance Parsing
> Ref: docs/design.md (Section 2.2)
- [ ] Configure `System.Text.Json` Source Generators for recipe and data models.
- [ ] Implement `Utf8JsonScanner` for schema discovery without object allocation.
- [ ] Implement `LazyTransformer` with an immutable Action Stack.

## Phase 4: TUI Explorer (Terminal.Gui v2.0)
> Ref: docs/spec.md (Section 3.2) & docs/design.md (Section 2.4)
- [ ] Initialize `Terminal.Gui` v2.0 application shell.
- [ ] Implement `VirtualGridView` for lag-free scrolling of millions of records.
- [ ] Create `ViewManager` to bridge the `Engine` and `TUI` layers.
- [ ] Implement interactive column morphing (Rename, Delete, Cast).

## Phase 5: Automation & Recipes
> Ref: docs/spec.md (Section 3.3)
- [ ] Implement `RecipeManager` for YAML serialization of the Action Stack.
- [ ] Create CLI headless mode for batch processing using saved recipes.

## Phase 6: Optimization & Polish
> Ref: docs/spec.md (Section 4)
- [ ] Conduct performance audit using BenchmarkDotNet and Native AOT toolchain.
- [ ] Finalize documentation (README.md, API docs).
- [ ] Perform binary size optimization for Native AOT.
