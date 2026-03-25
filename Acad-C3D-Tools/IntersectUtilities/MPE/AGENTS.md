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
