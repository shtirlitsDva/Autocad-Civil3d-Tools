# MPE Guardrails

## Scope
- These instructions apply to everything under `Acad-C3D-Tools/IntersectUtilities/MPE`.
- All file creation, editing, renaming, moving, deletion, and generated output must stay inside the `MPE` directory.
- Files and directories outside `MPE` are read-only for this scope and must not be changed.

## GitHub Push Protection
- Do not run `git push`, `gh repo sync`, `gh pr merge`, or any other command that publishes local changes to GitHub unless the user explicitly requests that action in the current conversation.
- A general request to "commit", "sync", "ship", or "finish" is not sufficient. The instruction must clearly mention pushing or publishing to GitHub.
- If there is any ambiguity about whether publishing is allowed, do not push.

## Execution Rules
- Prefer commands that operate only on paths inside `MPE`.
- Before any write operation, confirm the target path resolves inside `MPE`.
- Do not modify repository-wide configuration, hooks, workflows, or files outside `MPE` as part of work in this scope.

## Command Implementation Rules
- AutoCAD command methods must be wrapped in a `try`/`catch` block.
- When a command uses a transaction, create the transaction first, execute the command body inside `try`, call `tx.Commit()` only on success, and call `tx.Abort()` in `catch` before returning.
- In `catch`, log the exception with the project's existing debug/logging mechanism instead of swallowing the error silently.
- Structure new command methods to follow the pattern used in `Acad-C3D-Tools/IntersectUtilities/MSM/MSMScripts.cs`: acquire document/database context up front, start the transaction, run the command body inside `try`, handle failures in `catch`, and commit only after successful execution.
- Prefer clear command layout with a short setup section, the main command body inside `try`, and helper methods factored out below the command when needed.
- AutoCAD command methods must include XML-style command documentation above the method, following the pattern in `Acad-C3D-Tools/IntersectUtilities/MSM/MSMScripts.cs`.
- Command documentation must include `/// <command>...</command>`, `/// <summary>...</summary>`, and `/// <category>...</category>`.
- The `summary` must be meaningful and specific, not placeholder text. It should describe what the command does, important behavior, and key effects or constraints that a future maintainer needs to know.
- Additional behavioral notes that appear in the summary pattern from `MSMScripts.cs` should be preserved in new commands as documentation, especially when the command has manual follow-up steps, selection rules, trimming/retention rules, data-preservation guarantees, aliases, or other non-obvious behavior.

# Worktree Workflow
- For MPE work, prefer a dedicated git worktree so changes stay isolated from `master`.
- Create the worktree correctly the first time and avoid moving it later unless absolutely necessary.
- Prefer a shallow worktree path. In this repo, deeper worktree paths can break relative file links in `Acad-C3D-Tools/IntersectUtilities/IntersectUtilities.csproj`, especially links to files under `DamgaardRI/DimensioneringV2`.

### Recommended Naming
- Use one clear shared name for both the folder and the branch when possible.
- Prefer a short, shallow folder name such as `C:\Users\mpe\Github\shtirlitsDva\Autocad-Civil3d-Tools-MPE_<TaskName>_<DDMMYYYY>`.
- Prefer a matching branch name such as `Autocad-Civil3d-Tools-MPE_<TaskName>_<DDMMYYYY>`.

### Recommended Process
1. Start in the main repository root: `C:\Users\mpe\Github\shtirlitsDva\Autocad-Civil3d-Tools`.
2. Read `Acad-C3D-Tools/IntersectUtilities/MPE/AGENTS.md` before creating code or files.
3. Choose the final worktree folder name and branch name up front so no later rename is needed.
4. Create the worktree from `master` with the final branch name and final folder path in one command.
5. Open the new worktree folder in the IDE.
6. Do all MPE code changes only inside `Acad-C3D-Tools/IntersectUtilities/MPE`.
7. Keep files outside `MPE` read-only unless the user explicitly asks for broader repo changes and the scope is re-evaluated.
8. Build and test from the worktree, not from `master`.

### Why This Matters
- Creating the worktree at the final shallow path avoids broken relative includes.
- Matching the branch and folder names makes it easier to understand which worktree belongs to which task.
