---
name: feature-developer
description: >
  Guide the DataMorph feature/fix development lifecycle: write a design doc,
  then implement in two gated steps (skeleton first, logic second). Skeleton
  and logic quality gates are handled automatically by spawning code-reviewer
  and qa-advisor subagents; the user is only asked to approve the design and
  confirm the final implementation before committing.

  Always trigger when the user says anything like: "start a new feature", "pick
  up a GitHub issue", "let's work on a task", "begin development", "start
  implementation", "create a branch for the next issue", "what should I work on
  next", "implement the next task", or any request to begin or continue a
  feature/fix development cycle in this project.

  Do NOT trigger for: code reviews (use code-reviewer skill), test quality
  questions (use qa-advisor skill), or general C# questions unrelated to
  starting new development work.
---

# DataMorph Feature Developer

You are guiding a structured development workflow. Design approval comes from
the user; code quality gates are automated via subagent reviews. This keeps
design and implementation aligned while letting the AI-driven review loop
catch issues before the user ever sees the code.

The workflow has two phases. Work through them in order. The only hard user
gates are: **design approval** (end of Phase 1) and **final confirmation**
(end of Phase 2 Step 2). Everything else resolves automatically.

---

## Phase 1: Design

### 1-1. Write the design document

Create a design document under `./docs/` (filename: `docs/design_{task-summary}.md`).
The document follows an ADR-inspired structure — it captures not just *what*
was decided but *why*, so future contributors understand the reasoning without
having to reconstruct it from the code.

Cover the standard design content (requirements, files to change,
implementation approach), then close with a **Decision Record** section:

```markdown
## Decision Record
### Rationale
Why was this approach chosen? Explain the key reasons.

### Alternatives Considered
List at least one alternative and explain why it was rejected
(performance, complexity, AOT compatibility, testability, etc.).

### Consequences
What becomes easier or harder as a result of this decision?
Note any trade-offs, future constraints, or follow-up work this creates.
```

**Do not include the Issue number in the design document.**
Write the document entirely in English (project policy).

### 1-2. Commit, push, and await design approval

Commit the design document using Conventional Commits format:

```
docs: design {brief task description}
```

Push the branch and ask the user to review the design document.

> **Gate: do not start Phase 2 until the user explicitly approves the design.**

---

## Phase 2: Implementation

### Step 1 — Skeleton

**Only start after design approval.**

#### 2-1-a. Write skeleton code

Create all classes, interfaces, and methods that the design calls for:

- Method bodies must contain only `throw new NotImplementedException();`
- Test methods must contain only the three AAA comments:
  ```csharp
  // Arrange

  // Act

  // Assert
  ```
- Match namespaces, access modifiers, and naming exactly to what you committed
  in the design document — the skeleton is an API contract.

#### 2-1-b. Verify skeleton compiles

Run *only* these two commands (no tests yet — the implementations are stubs):

```bash
dotnet format
dotnet build
```

Fix any compilation errors or format diffs before continuing. The build must
produce **zero warnings** (`TreatWarningsAsErrors` is enabled).

#### 2-1-c. Automated skeleton review loop

Spawn **two subagents in parallel** — one running the `code-reviewer` skill,
one running the `qa-advisor` skill — to review the skeleton (declarations,
naming, API shape, test method naming, and suite completeness). Neither
reviewer should flag `NotImplementedException` bodies or empty test bodies as
issues; those are intentional at this stage.

Repeat until both reviewers report nothing actionable:

1. Collect findings from both subagents.
2. Fix all findings — Critical Issues/Gaps first, then Warnings, then
   Suggestions. Use judgment on Suggestions: skip only if there is a clear
   reason the suggested change would make the code worse in this context.
3. Re-run `dotnet build` to confirm the fixes compile cleanly.
4. Spawn both reviewers again in parallel.

When both reviewers are clean, **do not commit** — proceed directly to Step 2.
No user approval is needed here; the review loop is the gate.

---

### Step 2 — Logic

**Enter immediately after the skeleton review loop (2-1-c) passes.**

#### 2-2-a. Implement

Fill in every `throw new NotImplementedException()` body and every
`// Arrange / // Act / // Assert` test stub. Follow all project rules:

- Read `.claude/rules/csharp-standards.md` for C# style
- Read `.claude/rules/safety-and-nullability.md` for null safety and thread safety
- Read `.claude/rules/performance-and-aot.md` for allocation and AOT constraints
- Read `.claude/rules/testing.md` for test conventions

#### 2-2-b. Validate

Run validation in this order and **fix every failure before the next step**:

1. **Self-review**: re-read the design document and check your implementation
   against it. Also check for potential race conditions — even if the current
   entry point is single-threaded, this code could later be called from
   multiple threads.

2. **Format**:
   ```bash
   dotnet format
   ```
   Inspect the diff. Revert or accept each change deliberately.

3. **Tests**:
   ```bash
   dotnet test
   ```
   All tests must pass. Debug and fix any failures before reporting.

#### 2-2-c. Automated implementation review loop

Spawn **two subagents in parallel** — one running the `code-reviewer` skill,
one running the `qa-advisor` skill — to review the full implementation.

Repeat until both reviewers report nothing actionable:

1. Collect findings from both subagents.
2. Fix all findings — Critical Issues/Gaps first, then Warnings, then
   Suggestions. Use judgment on Suggestions: skip only if there is a clear
   reason the suggested change would make the code worse in this context.
3. Re-run `dotnet format` and `dotnet test` to confirm the fixes are clean.
4. Spawn both reviewers again in parallel.

#### 2-2-d. Request user confirmation

**Do not commit.** Present a concise summary to the user:

- Files changed and what each does
- Key implementation decisions that deviate from or extend the design document
- Confirmation that `dotnet test` passes (all N tests)
- **code-reviewer summary**: issues found and resolved across all review
  iterations (e.g. "2 Warnings fixed: nullable annotation, allocation in
  hot path"), or "No issues found ✅" if the first round was clean
- **qa-advisor summary**: same format — what gaps or quality issues were
  found and addressed across iterations, or "No issues found ✅"
- Any Suggestions that were intentionally skipped, with the reason

Ask the user: "Automated reviews passed. Please confirm to proceed with
commit, push, and PR creation."

> **Gate: do not commit until the user explicitly confirms.**

---

## Commit, Push, and PR

**Perform all three steps immediately after user confirmation — no further
check-ins needed.**

### Commit

```
feat: {brief description of what the feature does}
```

Additional body lines are welcome but keep them to a short summary — no
implementation stage words.

### Push

```bash
git push origin HEAD
```

### Create Pull Request

Create the PR using the GitHub CLI. The PR body **must** include the closing
keyword so GitHub automatically closes the Issue on merge:

```bash
gh pr create \
  --title "feat: {task description}" \
  --body "$(cat <<'EOF'
{1–3 sentence summary of what this PR does and why}

Closes #{issue_number}
EOF
)"
```

Present the PR URL to the user, then immediately ask:

> "PR is up at {url}. Could you review it when you have a moment?"

---

## Reminder: Rules That Are Easy to Get Wrong

| Rule | Detail |
|---|---|
| **No Issue number in branch names** | Branch is `feature/add-csv-export`, not `feature/123-add-csv-export` |
| **No Issue number in design docs** | The docs track the what, not the ticket |
| **No stage words in commit messages** | Never: "skeleton", "stub", "NotImplementedException", "WIP" |
| **Zero warnings on build** | `TreatWarningsAsErrors` is enabled — treat warnings as errors |
| **Hard user gates** | Only two: design approval and final implementation confirmation |
| **Subagent reviews run in parallel** | Spawn code-reviewer and qa-advisor at the same time, not sequentially |
| **No commit between skeleton and logic** | Skeleton and logic land in one commit after the implementation review passes |
| **All files in English** | Code, comments, docs, commit messages — all English |
