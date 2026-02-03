# Multi-Agent Development with Claude Code and Git Worktrees

A comprehensive guide for running multiple Claude Code agents simultaneously on independent features using git worktrees for isolation.

<table-of-contents>

1. [Introduction](#introduction)
2. [Understanding Git Worktrees](#understanding-git-worktrees)
3. [Step-by-Step Setup](#step-by-step-setup)
4. [Workflow Patterns](#workflow-patterns)
5. [Session Management](#session-management)
6. [Best Practices](#best-practices)
7. [Troubleshooting](#troubleshooting)
8. [Quick Reference](#quick-reference)

</table-of-contents>

<introduction>

## Why Git Worktrees + Multiple Agents?

When developing multiple independent features, running separate Claude Code agents in parallel can dramatically increase throughput. However, having multiple agents edit the same files creates merge conflicts and chaos.

**Git worktrees solve this** by providing complete filesystem isolation:
- Each worktree is a separate directory with its own working copy
- Changes in one worktree don't affect others
- All worktrees share the same Git history and remotes
- Perfect for parallel Claude Code sessions

**Benefits:**
- 3x+ faster development when features are independent
- No interference between Claude instances
- Easy merging via standard Git workflows
- Resume any session at any time

</introduction>

<understanding-git-worktrees>

## Understanding Git Worktrees

### What is a Git Worktree?

A git worktree lets you check out multiple branches simultaneously into separate directories. Unlike cloning the repo multiple times, worktrees:

- Share the same `.git` repository data
- Share remotes and fetch/push to the same origin
- Have independent working directories with isolated files
- Allow different branches checked out at the same time

### Directory Structure Example

```
H:\GitHub\shtirlitsDva\
├── Autocad-Civil3d-Tools\          # Main worktree (master)
├── Autocad-Civil3d-Tools-feature-a\ # Worktree for feature A
└── Autocad-Civil3d-Tools-feature-b\ # Worktree for feature B
```

Each directory is fully independent - files edited in `feature-a` don't appear in `feature-b` until merged.

</understanding-git-worktrees>

<step-by-step-setup>

## Step-by-Step Setup

### Step 1: Understand Your Current State

Check existing worktrees:
```bash
git worktree list
```

Output shows your current state:
```
H:/GitHub/shtirlitsDva/Autocad-Civil3d-Tools  a4d9ad3 [master]
```

### Step 2: Create Worktrees for Each Feature

**Option A: Create with new branches**
```bash
# From your main repo directory
cd H:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools

# Create worktree for Feature A
git worktree add ../Autocad-Civil3d-Tools-feature-a -b feature-a

# Create worktree for Feature B
git worktree add ../Autocad-Civil3d-Tools-feature-b -b feature-b
```

**Option B: Create with existing branches**
```bash
# If branches already exist
git worktree add ../Autocad-Civil3d-Tools-feature-a feature-a
git worktree add ../Autocad-Civil3d-Tools-feature-b feature-b
```

### Step 3: Initialize Development Environment in Each Worktree

Each worktree needs its own dependencies. For this .NET/C# project:

```bash
# In each worktree directory
cd H:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools-feature-a
dotnet restore

cd H:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools-feature-b
dotnet restore
```

**Common environment setup by stack:**

| Stack | Command |
|-------|---------|
| .NET | `dotnet restore` |
| Node.js | `npm install` or `yarn` |
| Python | `python -m venv venv && venv\Scripts\activate && pip install -r requirements.txt` |
| Go | `go mod download` |

### Step 4: Launch Claude Code in Each Worktree

**Terminal 1 - Feature A:**
```bash
cd H:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools-feature-a
claude
```

**Terminal 2 - Feature B:**
```bash
cd H:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools-feature-b
claude
```

Each Claude instance now operates in complete isolation!

### Step 5: Name Your Sessions (Important!)

Inside each Claude session, name it for easy resumption:

```
> /rename feature-a-implementation
```

```
> /rename feature-b-bugfix
```

</step-by-step-setup>

<workflow-patterns>

## Workflow Patterns

### Pattern 1: Parallel Independent Features

Best for: Features that touch different files/modules.

```
┌─────────────────────────┐    ┌─────────────────────────┐
│ Worktree: feature-a     │    │ Worktree: feature-b     │
│ Branch: feature-a       │    │ Branch: feature-b       │
│                         │    │                         │
│ Claude Agent 1          │    │ Claude Agent 2          │
│ Working on: Auth module │    │ Working on: Export      │
└─────────────────────────┘    └─────────────────────────┘
            │                              │
            └──────────┬───────────────────┘
                       ▼
              Main repo (master)
              Merge when ready
```

**Workflow:**
1. Create worktrees for each feature
2. Run Claude in each worktree
3. Each Claude commits to its branch
4. Merge branches to master when features complete

### Pattern 2: Writer/Reviewer Split

Best for: Quality-critical changes needing independent review.

```
Worktree 1 (Writer)          Worktree 2 (Reviewer)
─────────────────────        ─────────────────────
Claude implements feature    Claude reviews code
Commits changes              Provides feedback via PR comments
Creates PR                   No access to writer's session
```

**Benefit:** The reviewer Claude has no context from implementation, providing unbiased review.

### Pattern 3: Long-Running Task + Immediate Work

Best for: When one feature needs extended Claude attention.

```
Worktree: feature-complex         Worktree: main
──────────────────────────        ──────────────
Claude works on complex           You continue development
refactoring (may take 30min)      or run another Claude
                                  for quick fixes
```

**Benefit:** Don't block your workflow waiting for complex operations.

</workflow-patterns>

<session-management>

## Session Management

### Naming Sessions

Always name your sessions immediately:
```
> /rename descriptive-task-name
```

Good naming examples:
- `auth-oauth2-implementation`
- `ntrexport-topology-refactor`
- `dimensionering-ui-improvements`

### Resuming Sessions

**From command line:**
```bash
# Resume specific session
claude --resume auth-oauth2-implementation

# Continue most recent session in current directory
claude --continue

# Open session picker
claude --resume
```

**From inside Claude:**
```
> /resume auth-oauth2-implementation
```

### Session Picker Shortcuts

| Shortcut | Action |
|----------|--------|
| `↑`/`↓` | Navigate sessions |
| `Enter` | Resume selected |
| `P` | Preview session |
| `R` | Rename session |
| `/` | Search/filter |
| `B` | Filter by current branch |
| `A` | Toggle current dir/all projects |

### Cross-Worktree Session Visibility

The `/resume` picker shows sessions from **all worktrees** of the same repository. Sessions are tagged with their originating directory.

</session-management>

<best-practices>

## Best Practices

### 1. Plan Before Parallelizing

Assess if tasks are truly independent:

| ✅ Good for Parallel | ❌ Keep Sequential |
|---------------------|-------------------|
| Different modules | Same file sections |
| Independent features | Dependent outputs |
| Different layers (UI/backend) | Shared state changes |
| Non-conflicting files | Database schema + code using it |

### 2. Use Descriptive Worktree Names

```bash
# Good: Describes the work
git worktree add ../project-oauth-integration -b feature/oauth

# Bad: Generic
git worktree add ../worktree1 -b branch1
```

### 3. Initialize Each Worktree Properly

Create a checklist for your project:

```bash
# Example: After creating worktree
cd ../new-worktree
dotnet restore          # Dependencies
code .                  # Open IDE
claude                  # Start Claude
/rename task-name       # Name the session
```

### 4. Use Plan Mode for Safety

Enable Plan Mode when uncertain about changes:

```bash
# Start in plan mode
claude --permission-mode plan
```

Or toggle during session with `Shift+Tab` twice.

Plan Mode means Claude analyzes and plans without modifying files until you approve.

### 5. Regular Worktree Maintenance

```bash
# List all worktrees
git worktree list

# Remove completed worktree
git worktree remove ../project-feature-a

# Clean up stale worktree references
git worktree prune
```

### 6. Handle Shared Configuration

For files not in git (`.env`, local configs), you may need to copy them:

```bash
# Copy environment files to new worktree
copy .env ..\Autocad-Civil3d-Tools-feature-a\.env
```

### 7. Merge Strategy

When parallel work completes:

```bash
# From main worktree
cd H:\GitHub\shtirlitsDva\Autocad-Civil3d-Tools

# Fetch all branches
git fetch --all

# Merge feature branches
git checkout master
git merge feature-a
git merge feature-b

# Or create PRs for review
gh pr create --head feature-a --title "Feature A implementation"
```

</best-practices>

<troubleshooting>

## Troubleshooting

### "Branch already checked out" Error

```
fatal: 'feature-a' is already checked out at 'H:/path/to/worktree'
```

**Solution:** Each branch can only be checked out in one worktree. Either:
- Use the existing worktree
- Create a new branch: `git worktree add ../new-path -b new-branch-name`

### Worktree Shows Wrong/Outdated Code

The worktree is at a specific commit. To update:
```bash
cd ../worktree-directory
git pull origin master
# or
git fetch && git rebase origin/master
```

### Dependencies Missing in New Worktree

Each worktree needs its own `node_modules`, `.venv`, `bin/obj` folders:
```bash
cd ../new-worktree
dotnet restore  # or npm install, etc.
```

### Can't Delete Worktree

```bash
# Force remove if stuck
git worktree remove --force ../old-worktree

# Then prune
git worktree prune
```

### Claude Sessions Don't Show in Picker

Ensure you're in a git repository. The session picker groups by git repo, so sessions from worktrees of the same repo should appear together.

### Merge Conflicts Between Features

If features unexpectedly overlap:
1. Merge one feature to master first
2. Rebase the second feature: `git rebase master`
3. Resolve conflicts in the second worktree
4. Then merge to master

</troubleshooting>

<quick-reference>

## Quick Reference

### Essential Commands

```bash
# Create worktree with new branch
git worktree add ../path-to-worktree -b branch-name

# Create worktree with existing branch
git worktree add ../path-to-worktree existing-branch

# List all worktrees
git worktree list

# Remove worktree
git worktree remove ../path-to-worktree

# Clean up stale references
git worktree prune

# Start Claude in worktree
cd ../worktree-path && claude

# Name Claude session
/rename my-task-name

# Resume session
claude --resume task-name
claude --continue  # most recent

# Enable plan mode
claude --permission-mode plan
# or Shift+Tab twice during session
```

### Windows PowerShell Helper Function

Add to your PowerShell profile (`$PROFILE`):

```powershell
function New-Worktree {
    param(
        [Parameter(Mandatory=$true)]
        [string]$BranchName,
        [string]$BasePath = ".."
    )

    $repoName = (Get-Item .).Name
    $worktreePath = Join-Path $BasePath "$repoName-$BranchName"

    git worktree add $worktreePath -b $BranchName

    Write-Host "Worktree created at: $worktreePath" -ForegroundColor Green
    Write-Host "To start Claude there:" -ForegroundColor Yellow
    Write-Host "  cd $worktreePath && claude" -ForegroundColor Cyan
}

# Usage: New-Worktree feature-auth
```

### Checklist: Starting Parallel Development

- [ ] Identify independent features
- [ ] Create worktree: `git worktree add ../repo-feature -b feature`
- [ ] Navigate: `cd ../repo-feature`
- [ ] Restore dependencies: `dotnet restore` / `npm install`
- [ ] Start Claude: `claude`
- [ ] Name session: `/rename feature-description`
- [ ] Repeat for second feature in new terminal

### Checklist: Completing Parallel Development

- [ ] Commit changes in each worktree
- [ ] Push branches: `git push -u origin branch-name`
- [ ] Create PRs or merge to master
- [ ] Remove worktrees: `git worktree remove ../path`
- [ ] Delete merged branches: `git branch -d feature-branch`

</quick-reference>

<sources>

## Sources

- [Claude Code Official Documentation - Common Workflows](https://code.claude.com/docs/en/common-workflows)
- [Git Worktree Official Documentation](https://git-scm.com/docs/git-worktree)
- [Shipping Faster with Claude Code and Git Worktrees - incident.io](https://incident.io/blog/shipping-faster-with-claude-code-and-git-worktrees)
- [Parallel AI Coding with Git Worktrees - Agent Interviews](https://docs.agentinterviews.com/blog/parallel-ai-coding-with-gitworktrees/)
- [Running Multiple Claude Code Sessions with git worktree - DEV Community](https://dev.to/datadeer/part-2-running-multiple-claude-code-sessions-in-parallel-with-git-worktree-165i)
- [Mastering Git Worktrees with Claude Code - Medium](https://medium.com/@dtunai/mastering-git-worktrees-with-claude-code-for-parallel-development-workflow-41dc91e645fe)
- [Crystal - Parallel AI Session Manager](https://github.com/stravu/crystal)

</sources>
