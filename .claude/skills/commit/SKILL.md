---
name: commit
description: >
  Automate the DataMorph commit workflow: run dotnet format, build (verify zero
  warnings), and test, then generate a Conventional Commit message in English
  and commit changes automatically.

  Always trigger when the user says: "commit", "commit my changes", "git
  commit", "save my work to git", or any request to commit the current
  staged or unstaged changes to the repository.
---

# Commit Skill

Use this skill to perform a standardized commit process for the DataMorph project. This ensures that all commits meet the project's quality standards and follow the Conventional Commits specification.

## High-Level Process

1.  **Stage Changes**: Ensure all intended changes are staged (or will be staged).
2.  **Verify Code Quality**:
    -   Run `dotnet format` to ensure consistent styling.
    -   Run `dotnet build` and ensure there are **Zero Warnings**.
    -   Run `dotnet test` and ensure all tests pass.
3.  **Generate Commit Message**:
    -   Follow **Conventional Commits** (e.g., `feat: ...`, `fix: ...`).
    -   Must be written in **English**.
4.  **Execute Commit**: Once all checks pass, run `git commit -m "<message>"`.

## Detailed Steps

### 1. Preparation
Check the status of the repository and identify the changes to be committed.
-   Use `git status` and `git diff --cached`.

### 2. Quality Checks
Perform the following commands in the project root:
-   `dotnet format`
-   `dotnet build` (Verify: "0 Warning(s)")
-   `dotnet test` (Verify: "Passed!")

If any of these steps fail, stop and report the errors to the user. Do not proceed to commit.

### 3. Commit Message Generation
Analyze the staged changes and generate a concise, descriptive commit message in English.

**Format**: `<type>: <description>`

**Types**:
-   `feat`: A new feature
-   `fix`: A bug fix
-   `docs`: Documentation only changes
-   `style`: Formatting, missing semi colons, etc; no code change
-   `refactor`: Refactoring production code
-   `perf`: Code change that improves performance
-   `test`: Adding missing tests, refactoring tests; no production code change
-   `chore`: Updating build tasks, package manager configs, etc; no production code change

### 4. Execution
Run:
`git commit -m "<generated_message>"`

## Reference
-   [.claude/rules/language-and-git.md](../../rules/language-and-git.md)
-   [.claude/rules/commands.md](../../rules/commands.md)
