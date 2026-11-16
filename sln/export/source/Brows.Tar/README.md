# Tar archives

The Tar provider browses `.tar` archives, including their files and empty
directories. Provider IDs use explicit fields so archive files and directories
with the same name remain distinct:

```text
tar:1|f=C:\archives\files.tar|d=
tar:1|f=C:\archives\files.tar|d=docs
tar:1|f=C:\archives\files.tar|d=folder.tar
tar:1|f=C:\archives\files.tar|n=folder/inner.tar|d=docs
```

`f` is the outer filesystem archive, each `n` is a nested archive member, and
`d` is a directory inside the innermost archive. Empty `d` identifies the
archive root. Field values use UTF-8 percent escapes for reserved characters;
for example, a directory named `a>b` is written as `d=a%3Eb`. The explicit
`n` field identifies nested archives, so a directory named `folder.tar` stays a
directory.

The filesystem Tar command creates archives and extracts them to disk.
Provider commands support opening, copying, deleting, clipboard transfer, and
previewing archive entries.

## Archive engine

This adapter never interops with native libraries. It uses the tar classes of
the runtime, `System.Formats.Tar`, which read and write `v7`, `ustar`, `gnu`,
and `pax` archives over the file's stream directly. Each archive is listed,
extracted, and rewritten through a `TarReader` that is opened over the innermost
archived file of a temporary staging folder. Listing metadata entries, long
names, and extended headers are resolved by the reader before the adapter sees
them.

Entry names preserve whatever separators and directory markers the archive
stores. They normalize both separators, so a name that is stored once with
forward slashes and once with backslashes still describes one place. Current
directory components (`.`) are ignored, and a `./` directory marker represents
the archive root. When multiple file records describe the same normalized name,
the browser lists and reads the last occurrence, including nested archives.

## Supported operations and limits

- Entries of files and directories are listed, opened, extracted, and replaced,
  including empty regular files. Deleting a selected file preserves a directory
  with the same name; deleting a selected directory preserves a same-name file.
  Link, device, and fifo entries are listed as files, but they hold no data and
  are not extracted or streamed.
- Updates rewrite the whole archive, preserving entries not replaced or
  deleted, while their bytes are streamed to a sibling staging file that
  replaces the archive only after a successful write and the final cancellation
  check. Archive entry names use ordinal, case-sensitive comparisons.
- No compression is applied. The format of a kept entry is preserved; newly
  added entries are written as `pax` records.
- Nested `.tar` archives are browsed directly as providers, including archives
  stored inside other archives. If an entry cannot be opened as a Tar provider,
  the Open command extracts it to a temporary file for the OS opener.
- Dropped files and directories use the current archive directory as their
  destination. Directory transfers include nested files and empty directories.

## Temporary files and cancellation

Stream input is copied with bounded buffers into owned temporary files before
the destination archive is locked. Nested `.tar` archives are extracted to
owned temporary files under `%TEMP%\Brows\Tar` while an operation browses or
rewrites them. Their staged files and directories are removed when the
operation ends.

Extraction writes to owned temporary siblings and commits them after decoding
succeeds. This adapter also stages individual entries on disk when exposing
their content as managed streams. Those files and their temporary directories
are removed when the stream-ready lease is disposed, or if preparation fails.

Cancellation is checked while the archive is listed and written and before the
staged output is committed. It can race the commit, so a canceled task does not
guarantee that the destination is unchanged. Whole-archive extraction may leave
files already committed before a later output failure or late cancellation.

The shared Windows clipboard service prepares stream entries as file-drop
paths. Successful clipboard staging is deliberately retained through clipboard
clearing and process exit so later paste consumers can still open those paths.
Failed preparation removes its staging and leaves the previous clipboard
intact.

## Build and focused tests

Use .NET SDK 10. From the repository root:

```powershell
$solutionDir = (Resolve-Path sln).Path + [IO.Path]::DirectorySeparatorChar
dotnet test sln/export/tests/Brows.Tar.Tests/Brows.Tar.Tests.csproj `
  --configuration Release -p:SolutionDir=$solutionDir
dotnet test sln/export/tests/Brows.FileSystem.Commands.Tar.Tests/Brows.FileSystem.Commands.Tar.Tests.csproj `
  --configuration Release -p:SolutionDir=$solutionDir
```

The Tar tests use real archive files of the test's own temporary directories,
which are created and removed by the tests. The projects are managed only, so
they build and test with `dotnet` without Visual Studio's native toolset.
