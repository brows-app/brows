# AGENTS.md

Guidance for AI coding agents working in the Brows solution.

## What this is

**Brows** is a Windows File Explorer replacement: a keyboard/command-driven, multi-panel
file browser built as a WPF desktop app on .NET 10 with a plugin ("export") architecture
and a small amount of native C for third-party library interop (libcurl, libgit2, libssh2).

- Solution root: `sln/` — `brows.slnx` (XML solution format; there is no `.sln`, and `*.sln` is git-ignored here).
- Repository root: one level up, contains `vcpkg/` (submodule), `lib/x64-windows/`, `scripts/`, `.github/`.
- The only solution platform is **x64**.

## Layout

| Path | Purpose |
| --- | --- |
| `sln/framework/source` | Platform-neutral core: composition, abstractions, commands, providers, panels, config, CLI, IPC. Everything else depends on this. |
| `sln/framework/tests` | NUnit tests for the framework. |
| `sln/framework/samples` | Runnable samples for `Brows.CLI` and `Brows.IPC`. |
| `sln/windows/source` | `Brows.Win32.*` — a generic Win32/COM/WPF interop library with **no Brows domain knowledge**. Types mirror the Win32/COM API names exactly (`SHFILEINFOW`, `IShellItem`, `kernel32`). |
| `sln/export/source` | Plugins. Each feature (FileSystem, Drives, Git, SSH, FTP, Zip, Url, Bookmark, …) ships as one or more DLLs loaded at runtime. |
| `sln/export/source/windows` | Windows/WPF-specific halves of those plugins. |
| `sln/export/tests` | NUnit tests for plugins. |
| `sln/app/source` | The shell: `brows` (entry point, `WinExe`), `Brows.Commander` (panel/command/palette model), `Brows.Commander.Windows` (WPF views), `Brows.App.Windows` (WPF `Application`, theme, splash). |
| `sln/props` | Shared MSBuild property files. |

Build output goes to `sln/out/<Configuration>/`, with plugins in `brows.export/` and native DLLs in `brows.native/x64-windows/`.

## Architecture

### Layering rules

```
Brows.Abstractions.IO ─┐
                       ├─> Brows.Framework ──> Brows.Framework.Windows
Brows.Composition   ───┘         │                      │
                                 v                      v
                        export/source/*      export/source/windows/*
                                 │                      │
                                 └──────> app/source/* <┘
```

- Cross-platform plugin code must **not** reference `Brows.Win32.*`, WPF, or anything under
  `export/source/windows`. It declares an `IExport` service interface instead, and a `.Win32`
  or `.Windows` project implements it.
- `Brows.Win32.*` must not reference `Brows.Framework` — it is a standalone interop library.

### Composition (read this before adding anything)

Composition is hand-written on top of MEF discovery. There is **no source generator** and
**no registration list**.

- `Brows.Composition.IExport` is declared `[InheritedExport(typeof(IExport))]`. A concrete,
  non-abstract class becomes discoverable simply by implementing any interface that derives
  from `IExport`. Do not add `[Export]` attributes or registration code.
- Key derived contracts: `ICommand`, `IProviderFactory`, `IProviderExport` /
  `IProviderExport<TProvider>`, `IFileSys`, plus feature-specific ones you define.
- Lifecycle hooks: `IExportAndVary`, `IExportAndInit` (async `Init`), `IExportAndKill`,
  and `[ImportsReadyCallback]` methods.
- **Property injection is implicit.** `ImportPopulator` fills any get/set property (including
  non-public accessors) whose type is an `IExport`, or `IEnumerable<T>` / `IReadOnlyList<T>` /
  `IReadOnlyCollection<T>` of one. `[Import]` is not used. Add `[ImportRequired]` to throw when
  an implementation is missing; the default is optional.

  ```csharp
  public IRenameDirectoryEntries Service { get; set; }   // populated automatically
  ```

- Provider-scoped services are resolved explicitly: `provider.Import<IBookmark>()`.
- At runtime the app scans `Brows.*.dll` in the program directory and in `brows.export`
  (`ProgramMain.ImportInfo`). Adding a plugin project is enough; getting its DLL into
  `out/<Configuration>/brows.export/` is handled by `props/export.build.props`.
- `Brows.Instantiation` is *not* the container — it is a small reflection/`Activator` helper.
- Tests use `Brows.Composition.Testing.ImportSandbox`, which resets global `Imports` and
  injects an explicit export list (`ImportInfo.Listed`) instead of scanning DLLs.

### Core abstractions

- `IProvider` — a navigable data source (a filesystem directory, a zip, an FTP host…).
  Created by an `IProviderFactory` for a given ID and panel.
- `IEntry` — one item inside a provider. `IEntryDataDefinition` defines a column/value;
  `IEntryObservation` is the collection/selection/sort state.
- `IPanel` / `IPanelCollection` — the visible panes; `Commander` owns the collection.
- `ICommand` / `Command` / `Command<TParameter>` — user-invokable actions. `ICommandContext`
  gives access to the panel, provider, selection, input, palette, and `Operate(...)` for
  scheduling cancellable background work with `IOperationProgress`.
- `IConfig<T>` — async-loaded, live-reloading configuration backed by `Domore.Conf`.
- GUI is MVVM-ish via `I*Controller` interfaces: the platform-neutral project declares
  `IPanelController`, the WPF project implements it.

## Recipes

### Add a command

Follow `export/source/Brows.FileSystem.Commands.Rename` as the reference implementation.

1. Create `sln/export/source/Brows.<Feature>.Commands.<Name>/` with a minimal SDK-style csproj.
   `props/export.build.props` already supplies `net10.0`, global usings, and the
   `Brows.Framework` reference — do not re-add them.
2. `Commands/<Name>Parameter.cs` — a plain class whose properties are annotated with
   `Domore.Conf.Cli` attributes (`[CliArgument]`, `[CliRequired]`, `[Conf("bg")]`).
3. `Commands/<Name>.cs` — `internal sealed class <Name> : FileSystemCommand<<Name>Parameter>`
   (or `Command<T>`). Override `Work(Context context)`; guard with
   `context.HasParameter(out var parameter)` etc. and return `context.Operate(async (progress, token) => ...)`
   for anything long-running.
4. `Exports/I<Name>Something.cs` — if the work is platform-specific, declare an
   `IExport` interface here and implement it in the matching `.Win32` project.
5. Resources (see below), and register the project in `brows.slnx`.

The command's name is derived from the type: concrete type name prefixed by the provider
name with the `Provider` suffix stripped — e.g. `Rename` on `FileSystemProvider` becomes
`FileSystem_Rename`. Triggers/keybindings are not declared in code; they come from
`trigger.conf`.

### Resources and localization

There are **no `.resx` files**. Localization uses extensionless embedded resource files under
`Resource/Culture/`:

- `_` — invariant-culture key/value lines, e.g. `Command_Description_FileSystem_Rename = Rename entries`.
- `_.s.Command_Conf_<CommandName>` — the commented `conf` snippet shown as inline command help.

Both must be listed explicitly in the csproj as `<EmbeddedResource Include="..." />` (and
removed from `<None>`). Lookup keys are `Command_Description_<Name>`, `Command_Help_<Name>`,
`Command_Conf_<Name>`.

### Native interop

- `brows_<feature>.vcxproj` builds a C (not C++) DLL: C17, `CompileAsC`, exceptions off,
  `PlatformToolset v145`, output to `out/<Configuration>/brows.native/x64-windows/`.
  All link against `brows_framework.lib`.
- `Brows.<Feature>.Native.csproj` is the managed side. It does **not** reference the vcxproj;
  they are coupled only by DLL name and exported C symbol names, so keep the C header and the
  C# declarations in sync manually. Solution-level `<BuildDependency>` entries enforce ordering.
- Both interop styles exist: `[DllImport(..., CallingConvention = Cdecl)]` (Url, SSH) and
  `[LibraryImport]` + `[UnmanagedCallConv]` (Git). Match the surrounding file.
- Handles derive from `Brows.Native.NativeType` (lazy handle creation, disposal + finalizer).
  Strings are pinned UTF-8 via `NativeString`.
- Third-party headers/libs come from `vcpkg` into `lib/x64-windows/`; `vcpkg.json` declares
  `libssh2`, `curl`, and `libgit2` (all with OpenSSL/SSH features).

## Build, test, run

Prerequisites: .NET SDK 10.0.x (pinned by `sln/global.json`, `rollForward: feature`) and
Visual Studio with the C++ x64 toolset (v145).

**Full solution build — requires Visual Studio MSBuild, not `dotnet build`**, because of the
C++ projects. Keep the developer-environment setup and the build in the same `cmd` process:

```powershell
& $env:ComSpec /c 'call "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat" >nul && cd /d F:\dev\me\brows-private\sln && msbuild brows.slnx /p:Configuration=Debug /p:Platform=x64 /restore /verbosity:minimal'
```

Roughly 30 s for a rebuild. Locate `VsDevCmd.bat` with
`& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath`
if the path above does not exist. `dotnet build brows.slnx` fails with `MSB4278`
(`Microsoft.Cpp.Default.props` unavailable), and `dotnet build --platform x64` is not a valid switch.

**Tests** (NUnit + Moq + `NUnit.Analyzers`, ~1,480 tests, ~45 s). `dotnet test brows.slnx`
runs the tests but exits 1 because of the same C++ limitation, so target projects directly.
Managed projects invoked outside the solution **must** be given `SolutionDir` with a trailing
separator, or the `$(SolutionDir)/props/...` imports resolve to the wrong drive root:

```powershell
cd F:\dev\me\brows-private\sln
Get-ChildItem framework\tests, export\tests -Recurse -Filter *.csproj | ForEach-Object {
    dotnet test $_.FullName -c Debug '-p:SolutionDir=F:\dev\me\brows-private\sln\' --logger 'console;verbosity=minimal'
}
```

Prefer running only the affected test project(s) while iterating.

**Run**: `sln/out/<Configuration>/brows.exe`. Default configuration and the `*.conf.default`
help templates live in `app/source/brows/brows.config/`.

## Conventions

`.editorconfig` lives at `sln/.editorconfig` (not the repository root) and is the
authority. Highlights:

- C#: 4-space indent, CRLF, **UTF-8 with BOM**, final newline, 120-column guideline.
  `.csproj`/`.props`/XML/YAML/Markdown: 2 spaces. `.conf`: 2 spaces, UTF-8 no BOM.
- **Explicit types, not `var`** — all three `csharp_style_var_*` options are `false`.
- File-scoped namespaces (warning). Braces required on multiline statements (warning).
- `using` directives go **outside** the namespace, are **not** grouped, and `System` is
  **not** sorted first — they are simply alphabetical.
- Explicit accessibility modifiers on non-interface members. Types are typically
  `internal sealed`; `sealed override` is used liberally.
- `ImplicitUsings` is **off**. Global usings are declared as `<Using>` items in
  `props/root.build.props` (a curated set of `System` type aliases) and
  `props/export.build.props` (the `Brows.*` namespaces). Plugin code can therefore use
  `Brows.Commands`, `Brows.Entries`, `Brows.Providers`, etc. without a `using`.
- C# 14 (`LangVersion 14.0`): the `field` keyword is used for lazy backing stores —
  `private X Y => field ??= new X();` — prefer it over hand-written `_X` fields in new code.
- Constructor arguments are validated with `?? throw new ArgumentNullException(nameof(x))`.
- Async methods take a `CancellationToken` and pass it along; `CA2016` is a **warning**.
- Tests: project `Brows.X.Tests`, class `SubjectTest` with `[TestFixture]`, methods named
  `Member_Behavior` with `[Test]`/`[TestCase]`, and NUnit constraint assertions
  (`Assert.That(x, Is.EqualTo(y))`).

### MSBuild

- Central package management is on (`sln/Directory.Packages.props`). Add a `<PackageVersion>`
  there and a version-less `<PackageReference>` in the project.
- Individual csproj files should stay nearly empty. Put shared settings in `sln/props/*.props`,
  which `Directory.Build.props` files chain in this order: `root` → (`export` | `tests`) → `windows`.
- Do not add project references that a `Directory.Build.props` already supplies
  (`Brows.Framework` for exports, `Brows.Framework.Windows` for windows exports).

### Git

- Commit subjects are short, capitalized, imperative sentences ending with a period:
  `Add file-system projects.`, `Clean up namespaces.`, `Update .editorconfig.`
- `.gitattributes` forces CRLF for nearly all text and routes binaries (DLL/EXE/PDB/LIB/images)
  through Git LFS. Do not commit build output; `sln/.gitignore` covers `bin`/`obj`/`out`.

## Gotchas

- Use `scripts\Publish-Brows.ps1` to build and publish the self-contained desktop application.
- There is no CI that builds or tests the desktop app. The only GitHub Actions workflow is
  unrelated to it, so a green repository says nothing about this solution — build and test locally.
- `scripts/replace.bat` and `Replace-WordInTree.ps1` do destructive in-place find/replace and
  file renames across a tree; they are unrelated to building Brows.
- `sln/spelling.exclusion.dic` is an empty IDE spell-checker file, not a build input.
