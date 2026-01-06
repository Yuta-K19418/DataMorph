---
paths:
  - "**/*"
---

# Language & Git Policy

## Language Policy
- **Documentation**: All documentation (including README, specs, design docs, and TASKS.md) must be written in **English ONLY**.
- **Comments**: All code comments (inline `//` and XML documentation comments `///`) must be written in **English**.
- **Commit Messages**: Must be written in **English**.

## Git Workflow
- **ALWAYS** run `dotnet format` and `dotnet test` BEFORE committing.
- Ensure the project compiles with **Zero Warnings** (`TreatWarningsAsErrors` is enabled).

## Conventional Commits
Follow the **Conventional Commits** specification for all commit messages.

### Format
```
<type>: <description>
```

### Types
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `style`: Changes that do not affect the meaning of the code (white-space, formatting, etc)
- `refactor`: A code change that neither fixes a bug nor adds a feature
- `perf`: A code change that improves performance
- `test`: Adding missing tests or correcting existing tests
- `chore`: Changes to the build process or auxiliary tools and libraries

### Example
```
feat: add SIMD-accelerated newline detection
```
