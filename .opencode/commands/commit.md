# /commit

Instruction: Analyze the staged changes and execute a git commit following the project's specific guidelines.

## Strict Rules
1. **Commit Message Format**: You MUST strictly follow the rules defined in `@docs/development_guidelines.md`.
2. **Model Usage**: Use `gemini-2.0-flash-lite` (or the specified Gemini 2.5 Flash Lite) logic to understand the intent of the changes and generate a high-quality message.
3. **Action Scope**: Execute ONLY `git commit`. Do NOT execute `git push` under any circumstances.
4. **No Side Effects**: Do NOT modify, delete, or revert any unstaged changes or untracked files. Your operation must be isolated to the staged area.

## Context
### Staged Changes (Diff)
!git diff --cached

### Commit Guidelines
@docs/development_guidelines.md

## Execution Steps
1. Read the staged diff and the guidelines from `@docs/development_guidelines.md`.
2. Generate a commit message that adheres to the rules (e.g., specific prefixes, conventional commits, etc.).
3. Run `git commit -m "[generated_message]"` and report the result.
5. **No Confirmation**: Do NOT ask for confirmation. Generate the best message based on the diff and execute the commit immediately.
6. **Silent Execution**: Report only the result of the commit (success/failure and the message used).
