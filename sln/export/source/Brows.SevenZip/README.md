# SevenZip archives

The SevenZip provider browses `.7z` archives, including their files and empty
directories. Provider IDs use explicit fields so archive files and directories
with the same name remain distinct:

```text
7z:1|f=C:\archives\files.7z|d=
7z:1|f=C:\archives\files.7z|d=docs
7z:1|f=C:\archives\files.7z|d=folder.7z
7z:1|f=C:\archives\files.7z|n=folder/inner.7z|d=docs
```

`f` is the outer filesystem archive, each `n` is a nested archive member, and
`d` is a directory inside the innermost archive. Empty `d` identifies the
archive root. Field values use UTF-8 percent escapes for reserved characters;
for example, a directory named `a>b` is written as `d=a%3Eb`. The explicit
`n` field identifies nested archives, so a directory named `folder.7z` stays a
directory.

The former `file.7z>directory` composite IDs are no longer supported. Replace
old composite SevenZip IDs saved in bookmarks, history, or configuration with
the new format. The filesystem SevenZip command creates archives and extracts
them to disk. Provider commands support opening, copying, deleting, clipboard
transfer, and previewing archive entries.

## Native boundary

The native-language policy in [AGENTS.md](../../../../AGENTS.md) allows C or C++
according to the third-party package's supported interfaces. This adapter uses
C++17 to implement the archive, stream, and callback interfaces of `7zip.dll`.
Exception handling is enabled for its C++ implementation.

All managed interop goes through `Brows.SevenZip.Native` to `brows_7z.dll`.
Only `brows_7z` accesses the 7-Zip interfaces or loads `7zip.dll`. Its public
[brows_7z.h](../brows_7z/brows_7z.h) header is C-compatible: it exposes opaque
archive handles, plain C data, callbacks, and error codes. C++ builds give the
exported functions C linkage. SDK interfaces and C++ helper classes remain
private to the adapter and do not need corresponding managed wrapper classes.

Native archive handles have a managed `SevenZipArchive` owner. Entry metadata
and UTF-8 names are copied into managed objects before the native owner is
disposed. Update requests keep their unmanaged buffers alive for the duration of
the call. Native resources use scope-bound ownership; exported functions and
callbacks translate C++ exceptions into error codes or HRESULTs. Managed
progress callbacks keep their delegate and state rooted until the synchronous
native call returns, contain managed exceptions, and forward cancellation.

The `7zip` vcpkg dependency supplies the SDK headers and engine DLL. The native
project copies `7zip.dll` beside `brows_7z.dll` in
`sln/out/<Configuration>/brows.native/x64-windows/`. The loader tries that sibling
DLL first and then the normal Windows DLL search path. The managed project does
not reference the native project directly; solution build dependencies order the
native build after `brows_framework`.

## Supported operations and limits

- The provider selects the 7z archive handler for listing, extraction, creation,
  and updates. Other formats supported by the engine are not exposed here.
- Updates rewrite the archive, preserving entries not replaced or deleted.
  Archive entry names use ordinal, case-sensitive comparisons.
- No password prompt or password callback is implemented. Encrypted archives and
  creating encrypted output are unsupported.
- Nested `.7z` archives are browsed directly as providers, including archives
  stored inside other archives. If an entry cannot be opened as a SevenZip
  provider, the Open command extracts it to a temporary file for the OS opener.

## Temporary files and cancellation

Stream input is copied with bounded buffers into owned temporary files before
the destination archive is locked. The native rewrite uses a unique sibling
temporary file, which replaces the archive only after a successful write and
the final cancellation check. Failed operations remove their staging files.

Nested `.7z` archives are extracted to owned temporary files while an operation
browses or rewrites them. Their staged files and directories are removed when
the operation ends.

Extraction writes to owned temporary siblings and commits them after decoding
succeeds. This adapter also stages individual entries on disk when exposing
their content as managed streams. Those files and their temporary directories
are removed when the stream-ready lease is disposed, or if preparation fails.
Disk staging is an adapter choice; the SDK supports output stream interfaces.

Cancellation is checked during native progress callbacks and before committing
staged output. It can race the commit, so a canceled task does not guarantee that
the destination is unchanged. Whole-archive extraction may leave files already
committed before a later output failure or late cancellation.

The shared Windows clipboard service prepares stream entries as file-drop
paths. Successful clipboard staging is deliberately retained through clipboard
clearing and process exit so later paste consumers can still open those paths.
Failed preparation removes its staging and leaves the previous clipboard intact.

## Build and focused tests

Use .NET SDK 10 and Visual Studio's v145 C++ x64 toolset. From the repository
root in a Visual Studio developer PowerShell session:

```powershell
msbuild sln/brows.slnx /p:Configuration=Release /p:Platform=x64 /restore /verbosity:minimal
```

The full solution requires Visual Studio MSBuild because it contains native
projects. See [AGENTS.md](../../../../AGENTS.md) for developer-environment setup.
After building the native DLLs, run the affected managed tests from the
repository root, preserving the trailing separator in `SolutionDir`:

```powershell
$solutionDir = (Resolve-Path sln).Path + [IO.Path]::DirectorySeparatorChar
dotnet test sln/export/tests/Brows.SevenZip.Tests/Brows.SevenZip.Tests.csproj `
  --configuration Release --no-restore "-p:SolutionDir=$solutionDir"
dotnet test sln/export/tests/Brows.FileSystem.Commands.SevenZip.Tests/Brows.FileSystem.Commands.SevenZip.Tests.csproj `
  --configuration Release --no-restore "-p:SolutionDir=$solutionDir"
```

The SevenZip tests use real archives and built native DLLs, including `7zip.dll`.
The test project copies those DLLs from the selected configuration's native
output directory. Tests create and clean up owned temporary directories; native
filesystem and case-sensitive-directory checks need normal filesystem access.
