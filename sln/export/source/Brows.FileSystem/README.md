# File-system terminal

Brows can host a PowerShell terminal beneath the entries of a file-system
provider. The terminal uses the published `Brows.TTY.Windows` 0.1.0 package and
Windows ConPTY.

## Commands

| Command | Input | Default shortcut | Effect |
| --- | --- | --- | --- |
| `FileSystem_TerminalOpen` | `terminal`, `term` | Ctrl+Alt+T | Show and focus the provider's terminal. |
| `FileSystem_TerminalRelease` | `terminal-release` | Ctrl+Shift+F12 | Return focus; the terminal keeps running. |
| `FileSystem_TerminalClose` | `terminal-close` | None | Stop the session and remove the terminal view. |

Commands are available from the command palette. The inputs and shortcuts are
defaults from `trigger.conf.default`; a user's `trigger.conf` can override them.
While the terminal has keyboard focus, the release shortcut is reserved for
returning focus to the entries; Escape and other keys continue to the terminal.

To move the release shortcut, add both lines below to `trigger.conf`, changing
F11 to the key you want:

```conf
cmd[FileSystem_TerminalRelease].press[CTRL+SHIFT+F12] = disabled
cmd[FileSystem_TerminalRelease].press[CTRL+SHIFT+F11] = *
```

The `disabled` value removes that inherited gesture from the command. Adding a
new key alone leaves the default shortcut active, and an empty value does not
remove it. The marker is case-insensitive, may have surrounding whitespace, and
must be the whole value. It applies to one expanded gesture key, so a grouped
binding can be disabled one key at a time.

## Focus and lower views

Opening the terminal acts on the active file-system provider, including an
empty directory. The starting directory is the provider's directory, never the
selected entry's path. Repeating Open focuses the same running session and
preserves its output and shell's current directory. It does not restart the
shell or send it a `cd` command.

The terminal and file preview share one lower-view area. Opening the terminal
replaces the preview. Showing the preview replaces and closes the terminal.
Closing the terminal does not restore a preview that it replaced. The
navigation tree remains available. Each provider instance owns its own session,
so panels do not share terminal state.

Navigating to another provider stops the old provider's terminal. Refreshing
entries on the same provider instance leaves it running. Removing the provider
or panel, or shutting down Brows, also closes its session. Shell commands that
change the shell's current directory do not navigate the Brows panel, and Brows
navigation does not inject `cd` commands into the shell.

## Shell and Windows requirements

The terminal starts Windows PowerShell from the Windows system directory with
`-NoLogo -NoProfile -NoExit`. Brows supplies the provider path as the process
working directory rather than inserting it into shell text. Startup fails if
the requested directory cannot be opened; Brows does not silently substitute
another directory.

Windows 10 version 1809 or later is required for ConPTY. The integration targets
Windows x64 and depends on the package's bundled native terminal and ConPTY
components.

## Exit, failure and retry

When PowerShell exits, the terminal stays visible with its final output and exit
status. Brows does not restart it automatically. Run
`FileSystem_TerminalOpen` again to start a fresh session in the provider's
original directory.

If startup fails, the file entries remain available and the terminal reports
the failure. Check that Windows PowerShell is present and that the provider
directory still exists and is accessible. Then run Open to retry, or run Close
to remove the failed terminal view. Startup never falls back to the application
directory. Closing the terminal stops the processes owned by that terminal
session.
