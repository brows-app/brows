# Git repository status

Run `status` from a filesystem directory to open the repository's Git-status provider in the current panel. The provider shows changes across the whole repository, including deleted paths. Its header shows the branch, tracking branch and local ahead/behind counts, summary, and any operation in progress.

Use `status "C:\path\to\repository"` to choose another directory, or `status where=panel` to open a new panel. Relative paths resolve from the current filesystem directory or Git-status repository. From another provider, supply a path. The provider is also addressable by `git-status:` followed by an absolute file URI, for example `git-status:file:///C:/projects/repo`.

The default columns are Path, Staged, Working tree, and Original path. Conflicts sort first, followed by path. Run `include-ignored` or press Ctrl+I to toggle ignored files and directories. Use `include-ignored true` to include them or `include-ignored false` to exclude them. Status refreshes every two seconds while the provider is active; Ctrl+R refreshes immediately. Existing rows and selections are retained while their paths remain in the view. A failed refresh reports the error and retains the last successful snapshot. Navigating to the parent returns to the repository directory.

The `status` and `include-ignored` inputs are registered in `trigger.conf.default`. If a custom `trigger.conf` overrides the defaults, add `cmd[Status].input = status`, `cmd[GitStatus_IncludeIgnored].input = include-ignored|includeignored`, and `cmd[GitStatus_IncludeIgnored].press[CTRL+I] = *` there.

The data-column picker exposes these Git columns for filesystem entries:

- `GitRepoHead`: branch, detached HEAD, or an unborn branch.
- `GitTrackingBranch`: upstream and locally known ahead/behind counts.
- `GitRepoState`: an operation in progress.
- `GitStatus`: combined status of a file or changes beneath a directory.
- `GitIndexStatus`: staged changes.
- `GitWorkTreeStatus`: unstaged, untracked, and ignored status.

All repository inspection goes through `Brows.Git.Native` to `brows_git`. Only the C project uses libgit2. Snapshots own their managed data and release native resources after reading. No repository mutation or network operation is exposed.

`GitStatusReader` provides repository snapshots with branch, tracking, local ahead/behind counts, operation state, and file changes. Ignored directories are collapsed during inspection. Bare repositories provide metadata without working-tree status.

## Tests

Build the solution with Visual Studio MSBuild (x64), then run:

```powershell
dotnet test sln/export/tests/Brows.Git.Tests/Brows.Git.Tests.csproj -c Debug -p:SolutionDir=F:\dev\me\brows-private\sln\
```

Adapt `SolutionDir` to the checkout. Tests require Git on PATH and built native DLLs in `sln/out/Debug/brows.native/x64-windows` (or `BROWS_GIT_NATIVE_DIR`). They create local repositories beneath a unique OS temporary directory, isolate Git command configuration, and delete only their own test directories. Git CLI mutations are limited to test fixtures. Tests check both repository state and that inspection leaves index, HEAD, and configuration unchanged.
