# File System Windows Export

This export receives `Brows.TTY.Windows` through `Brows.Framework.Windows`,
which references the published package, centrally pinned
at `0.1.0`. Its dependencies resolve to `Brows.TTY.Win32` and `Brows.TTY`, also at
`0.1.0`. The terminal commands use the shared provider lower view
and the WPF control described below.

## Terminal control

`FileSystemTerminalControl` derives from `ModelView<FileSystemTerminal>` and
implements `IFileSystemTerminalController` directly. The provider DataTemplate
binds its `ViewModel`. Attachment remains explicit so unload and template
replacement retain cleanup before detaching the model.

The visual tree lives in
`Resources/Styles/Brows.Style.FileSystemTerminal.xaml`, merged by
`Resource/Resource.xaml`. `OnApplyTemplate` acquires `PART_Terminal` and
`PART_Status`; startup remains explicit. Input ownership binds to the templated
parent's ViewModel only on the emulator subtree.

Template replacement invalidates queued focus and awaits old emulator cleanup
before another session starts. Failed old parts remain owned for cleanup retry.
An unrelated DataContext does not replace an explicitly supplied ViewModel.

## Verified package contract

The restored WPF asset targets `net10.0-windows7.0`, compatible with this project's
`net10.0-windows` target. The control is `Brows.Windows.Controls.TerminalEmulator`,
with XAML namespace `http://schemas.brows.app/winfx/2026/xaml/presentation`.

The dispatcher-bound API includes `CommandLine`, `WorkingDirectory`, `AutoStart`,
`StartAsync()`, `StopAsync()`, `IsRunning`, `Title`, `LastError`, `TerminalExited`
and `TerminalFailed`. Set `AutoStart=false` before loading when startup is managed
by the host. Explicit lifecycle failures throw; automatic startup failures raise
`TerminalFailed` and set `LastError`. Unloading stops the session asynchronously.

The packages document Windows 10 version 1809 or later for ConPTY and supply x64
and ARM64 native assets. Brows currently builds x64. The exact source is available
at `F:\dev\me\brows-tty`; the restored package repository metadata identifies
commit `c102b856f3cbc19371cb6225d99e41829216ed44`.

## Runtime deployment

The package reference and notice-copy metadata live in `Brows.Framework.Windows`.
The supported self-contained publish places `Brows.TTY.Windows.dll`,
`Brows.TTY.Win32.dll`, and `Brows.TTY.dll` in the application directory through
`Brows.Framework.Windows`. The export output also has those assemblies because
`Brows.FileSystem.Windows` uses the same package dependency. Package native runtime
trees are deployed beside both managed assembly locations; the loader resolves
relative to the loaded assembly, so both application-root and `brows.export` layouts
must be preserved. The clean publish smoke loaded the package assembly and native
modules from the application root. No `CopyLocalLockFileAssemblies` or
`UseCommonOutputDirectory` override is required in this export.

The x64 publish contains these native assets under `runtimes/win-x64/native`:

- `brows-tty-win32.dll`
- `brows-ghostty.dll`
- `ghostty-vt.dll`
- `conpty.dll`
- `OpenConsole.exe`

Package `buildTransitive` targets supply the native runtime trees. The loader probes
relative to its managed assembly, so preserve the application-root runtime layout.
The packages use MIT licensing and include third-party notices. Their targets do
not deploy the root notice files; `Brows.Framework.Windows` copies the package's
`LICENSE` and `THIRD-PARTY-NOTICES.txt` as `Brows.TTY.LICENSE` and
`Brows.TTY.THIRD-PARTY-NOTICES.txt` to the application directory for build and
publish. Fresh staging confirmed both notices there; they are not copied to
`brows.export`.

Brows scans `Brows.*.dll` in the program directory and `brows.export`. The TTY
source does not reference `Brows.Composition` or implement `IExport`, so those
assemblies add no feature registrations. The supported publish helper is
`sln/scripts/publish-brows.ps1`; its executable publish step deploys the shared
terminal dependency, native runtimes and notices before it copies the export tree.

## Verification (2026-11-09)

- Focused restore and Release build passed with the trailing `SolutionDir`
  property and binlogs; the focused build had zero warnings and errors.
- Full Visual Studio MSBuild Release/x64 solution builds passed, including the
  final deployment settings. Native dependencies reused the existing local
  `dev/vcpkg_installed` installation. The first build reported existing source and
  native cache-directory warnings.
- The executable publish step and the helper's equivalent plugin/native tree copy
  passed into a fresh staging directory. The three managed assemblies and both
  notice files matched the build output by hash; all five native assets were present.
- A disposable `net10.0-windows`, x64 WPF consumer compiled against `0.1.0` and ran
  against the actual plugin output in a fresh `brows.export` staging layout. It
  verified dispatcher startup, no automatic launch with `AutoStart=false`, a real
  PowerShell process observing a working directory containing spaces, keyboard
  focus, exit code 17 through `TerminalExited`, native module loading, repeated
  stop, explicit stop and unload terminating owned processes, reload without
  automatic restart, explicit startup exceptions, and automatic failure events.
  The consumer exited successfully. Its source, binlogs and runtime log remain
  local under `_agents/Phase1Consumer` and `_agents/Phase1Runtime`.
- Existing managed regression suites ran. Four tests failed: two FTP.Windows and
  two SFTP.Windows preview tests could not resolve `Brows.Style.ProviderGrid`.
  Other suites passed; prerequisite-dependent cases were skipped. FileSystem had
  50 passes and one skip; application tests had 17 passes. A rebuilt baseline
  comparison could not complete because the original worktree's reference assembly
  was invalid, so these failures are not asserted to be baseline failures.

After relocating the dependency to `Brows.Framework.Windows`, the full Release/x64
solution build and a fresh executable publish passed. All three managed assemblies,
five x64 native assets and both notices were present in the application-root layout.
The same WPF lifecycle consumer passed against those published assets. The framework
Windows tests (55) and application tests (17) also passed.

This phase does not verify terminal input routing inside Brows, UNC directories,
terminal rendering correctness, or interactive keyboard shortcuts. Those require
the later provider-view and command integration phases.

## Real package behavior and opt-in coverage

The FileSystem host assigns the provider's directory snapshot directly to the
package control's WorkingDirectory; it does not put the path into PowerShell
command text. PowerShell starts with -NoLogo -NoProfile -NoExit. The explicit
package fixture checks a literal directory containing spaces, an apostrophe, an ampersand, and ü,
a drive root, deleted-directory startup failure and retry, normal exit and
restart, shell-directory isolation, and independent panel sessions.

The test checks an existing UNC share only when HOMESHARE names an accessible
UNC directory. It attempts a directory whose listing is denied by the current
user, but skips if Windows still permits that directory as a process working
directory. These tests never create shares, change ACLs, or alter credentials.
If the environment has no suitable access boundary or UNC share, those cases
report a skip. This checkout had only local C: and F: drives and no configured
HOMESHARE, so UNC execution remains unverified here. Access-denied startup also
remains environment-dependent until the explicit case runs.

TerminalEmulator.ObserveExit drains final output and refreshes the terminal frame
before setting IsRunning to false and raising TerminalExited. The control keeps
that final frame visible after normal process exit and after StopAsync; StopAsync
releases the session and native resources without clearing the rendered rows. A
later StartAsync creates a new TerminalSession and engine, replacing the previous
session's output with a fresh buffer. The explicit integration fixture verifies
exit code reporting and process termination, attaches rendered frames after exit
and after the restarted shell answers a prompt probe, and confirms that closing
one of two panel sessions leaves the other running and usable. It changes the
shell directory and checks that repeat-open preserves that shell directory while
the Brows provider path remains unchanged. OnUnloaded calls StopAsync
asynchronously; the host awaits its cleanup path when it needs a completion
signal.

The package's Ghostty frame exposes per-cell foreground/background colors and
style attributes. TerminalDrawing.Brush maps packed ARGB values to frozen WPF
brushes, including ANSI color output. The real exit/restart test emits a red ANSI
sentinel and attaches screenshots after output drain and after the restarted
shell responds to a prompt probe. This verifies the package's color path without
claiming exhaustive escape-sequence compatibility.

The real-package fixture is Explicit because it launches PowerShell and ConPTY.
Run it only in an interactive Windows desktop with the published Brows.TTY.*
runtime assets deployed. The existing control fixture separately checks template
replacement and cleanup. These package tests do not substitute for the fake
lifecycle tests or the regular WPF regression suite.

## Phase 4 verification

The focused Release build passed with zero warnings and errors. The ordinary
Brows.FileSystem.Windows.Tests run passed; the package-launching Explicit
cases were selected separately.

Four real-package cases passed: the literal Unicode path, drive root, deleted
directory failure followed by successful retry after recreation, and exit/restart
with shell-directory and two-panel isolation. The exit/restart test attached
screenshots showing the red ANSI sentinel in the final frame and a fresh frame
after restart.

Two environment-dependent cases skipped. No accessible UNC path was configured
in HOMESHARE. Directories whose listing was denied by the current user still
worked as process working directories, so no access-denied startup failure
could be verified without changing permissions.

## Phase 5 deployment verification

On 2026-11-09, the supported `sln/scripts/publish-brows.ps1` helper completed a
Release/x64 full Visual Studio MSBuild rebuild and self-contained publish into a
new GUID-named directory under `%TEMP%`. It used the existing sibling
`dev/vcpkg_installed` cache through `VcpkgInstalledDir`, restored the environment
variable afterward, and recorded the helper output in `sln/out/Phase5-FinalPublish.log`.
The build produced six warnings in unchanged code (four CA2016 and two CS0067)
and no errors. The bundled `Microsoft.NETCore.App` and
`Microsoft.WindowsDesktop.App` runtime versions were both `10.0.13`.

The fresh app directory and its `brows.export` directory each contained the three
TTY managed assemblies and all five `win-x64` native assets. The application
folder contained `Brows.TTY.LICENSE` and `Brows.TTY.THIRD-PARTY-NOTICES.txt`;
managed and native files shared matching hashes across the two deployment layouts.

A temporary WPF composition probe was launched from the staged app directory with
an empty development DLL search path and the exact staged `brows.deps.json` and
`brows.runtimeconfig.json` manifests. Its resolver fallback was disabled. It
loaded 352 `IExport` instances, no `IExport` instances from `Brows.TTY.*`, and
found `TerminalOpen`, `TerminalRelease`, and `TerminalClose`. After using the app's
translation initialization, the shipped trigger configuration matched
`terminal|term` with Ctrl+Alt+T, `terminal-release` with Ctrl+Shift+F12, and
`terminal-close` without a default gesture. `Brows.Framework.dll` and
`System.Private.CoreLib.dll` loaded from the staged application directory.

The same staged probe launched the published TTY control with a controlled
PowerShell session. It verified the PowerShell working directory, sent and
observed an input marker through the terminal API, and resized the control.
PowerShell reported `Console.WindowWidth` changing from 88 to 113 columns through
its OSC title, confirming the native resize. The probe saw exit code 17, captured
a rendered terminal frame, and called `StopAsync` twice after exit. The final
transcript is `sln/out/Phase5-FinalAppDepsProbe.log`.

The package control assembly and all four loaded native modules
(`brows-tty-win32.dll`, `brows-ghostty.dll`, `ghostty-vt.dll`, and `conpty.dll`)
resolved from the staged application-root deployment tree. The probe ran hidden,
with isolated application-data variables, and did not launch `brows.exe`.

Input was routed through `TerminalEmulator.Paste` and a synthetic WPF Enter event.
This does not verify physical keyboard input, operating-system focus ownership,
clipboard shortcuts, foreground TUI interaction, or invoking the terminal through
the visible Brows provider UI. Those interactive checks remain unavailable in this
automated pass.

The final ordinary managed run passed 25 of 26 test projects. All six new
configuration/trigger cases passed. Framework.Tests reported two failures in
unchanged tests: `Subscribe_ChangedWithDifferentCase_RaisesChanged` timed out
and `Freed_InFlightLoad_FreesAfterItFinishes` failed its in-flight load assertion.
The watcher passed in isolation and with the six new tests excluded; the native
loader failure reproduced in both runs. These failures remain unresolved.
The final explicit real-package run passed six cases and skipped the two
environment-dependent UNC/access-denied cases.
