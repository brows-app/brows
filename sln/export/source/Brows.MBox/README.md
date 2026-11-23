# MBOX support

Brows reads a mailbox file as a list of messages, shows a preview of the current
message, and extracts the selected messages as Internet messages.

The feature is made of two assemblies:

| Project | Purpose |
| --- | --- |
| `Brows.MBox` | The provider, the commands, and the data of the messages. The parser of the mailbox format is in its `Brows.MBox.Format` namespace, which does not depend on the rest of Brows. |
| `Brows.MBox.Windows` | The WPF layer: the panel of the mailbox and its preview. |

## Opening a mailbox

A file whose name ends with `.mbox` is a mailbox. The extension is matched without
regard to case, and a compressed mailbox (`.mbox.gz`) is not a mailbox.

Select the file in a file-system panel and run `FileSystem_Open`. The panel then
lists the messages of the mailbox instead of the files of a directory. The mailbox
is identified by its path, and its parent is the directory that holds it, so `Up`
returns to that directory.

The bytes of the mailbox are never changed. Brows holds the file with a read
share, so other programs can read the mailbox while it is open, but a program
that writes to it fails until the panel is closed.

## The keyboard route

1. `FileSystem_Open` on a `.mbox` file opens the mailbox in the active panel.
2. The list shows the messages of the mailbox, newest first.
3. `MBox_View` shows the preview of the current message, and hides it again when
   it is run a second time.
4. `MBox_PreviewFocus`, or `Ctrl+Alt+P`, moves the keyboard focus into the
   preview, after the command palette has closed.
5. The preview supports the arrow keys, `PageUp`, `PageDown`, `Ctrl+Home`,
   `Ctrl+End`, the selection, and `Ctrl+C`.
6. `Escape` moves the focus back to the list of the messages.
7. `MBox_Export`, or `Ctrl+Alt+E`, exports archives of the selected messages and
   their attachments, or the messages alone, to another panel.
8. `Up` returns to the directory that holds the mailbox.

## The commands

| Command | Input | Parameters |
| --- | --- | --- |
| `MBox_Open` | `open` | None |
| `MBox_View` | `view` | `toggle=preview` |
| `MBox_Export` | `export` | `where`, then `archive` or `eml` |
| `MBox_PreviewFocus` | `mbox-focus` | `target=preview` or `target=entries` |

`MBox_Open` and `MBox_View` share the input and the shortcuts of the `Open` and
`View` commands of the other providers.

`MBox_View` takes the views to toggle, and does nothing when it is given none.
The `ALT+P` shortcut is bound to it with `toggle=preview`.

`MBox_Open` extracts the selected messages to files of an Internet message, and
opens them with the application that is associated with the file type.

`MBox_Export` exports the selected messages to the provider of a panel:

- `where` chooses the panel the same way that copy and move do: `auto`, `here`,
  `next`, or `previous`, or their first letters. `auto`, the default, chooses
  the other panel when there are exactly two panels.
- `mode` chooses the file that is exported for each message. `archive`, the
  default, exports a ZIP archive, `message-{offset}.zip`, that holds the message
  and its decoded attachments. `eml` exports the message as a file of an
  Internet message, `message-{offset}.eml`.
- Both arguments may be given by position, `where` first: `export n eml`. The
  mode alone is named, `export mode=eml`, because a first argument is always
  read as `where`.
- The files are named after the offset of the message in the mailbox. A subject,
  a sender, or a message ID is never used as a name. The name of an attachment is
  made safe: a path, a character that a file name cannot hold, or the name of a
  device is removed or replaced, and a repeated name is numbered, such as
  `report (2).pdf`. An attachment that the mailbox cuts short, or whose transfer
  encoding is not supported, is left out of the archive.
- The panel receives the files the way that it receives copied files, so any
  provider that can receive copied files, such as a directory, an archive, or a
  remote host, can receive them. The provider decides what happens to a file that
  already exists; a directory keeps the existing file and gives the new file a
  name of its own. The mailbox itself, which is read only, never receives them.

`MBox_PreviewFocus` moves the focus between the list of the messages and their
preview. The target `preview` is the default, and it shows the preview when it is
hidden; `Ctrl+Alt+P` is bound to it. The target `entries` moves the focus back to
the list, but only when the preview owns the focus, so `Escape` keeps its usual
meaning everywhere else.

## The columns

The list shows the columns `Subject`, `From`, `Date` and `Size` by default, and
the messages are sorted by date, newest first. The columns `To`, `Message ID`,
`Labels` and `Thread ID` are available as well. Thread ID uses `X-GM-THRID`,
with `X-Gmail-Thread-Id` as a fallback when the preferred header is empty or absent.

A message without a subject shows `(no subject)`. The date is shown in UTC as
`yyyy-MM-dd HH:mm:ss 'UTC'`, and a message without a date shows no date at all.
The size is the length of the stored record, so it includes the envelope of the
message and its framing.

## The state of the mailbox

The panel shows the state of the mailbox in its header, even when the mailbox
holds no message.

| State | Meaning |
| --- | --- |
| Loading | The mailbox is being read. |
| Ready | Every message was indexed. |
| Empty | The mailbox holds no message. |
| Partial | A limit stopped the index, so only some of the messages are listed. |
| Cancelled | The read of the mailbox was cancelled. |
| Failed | The mailbox could not be read. The reason is shown next to it. |

A file that holds bytes but no valid message is not an empty mailbox, but a
malformed one, and it is reported as failed.

The index is bounded. The mailbox is indexed until it holds a million messages,
or until the metadata of the indexed messages reaches 64 MiB. A mailbox that is
larger than that shows the state `Partial`, and the messages that were not
indexed are not listed.

## The preview and the extraction

The preview is bounded, and the extraction is not. The preview reads at most
8 MiB of the stored body of a message, and it keeps at most 8 MiB of decoded text,
and at most 1,048,576 characters of it. The cache of the preview keeps at most
8 previews, and at most 32 MiB of them, and at most two previews are decoded at
the same time. Text budgets count UTF-16 bytes across all retained parts, and the
cache counts both plain text and HTML. The active decode limit must be positive;
changes to it also apply to queued preview requests. A message longer than a
preview limit is cut short, and the preview says so.

The extraction reads the whole message, and writes it out exactly as it is
stored: the headers, the line endings, the MIME structure, and the attachments
are preserved, and the body is unquoted as the dialect of the mailbox requires.
A preview that was cut short has no effect on the extraction, and the extraction
never rebuilds a message from the preview.

A message that holds no plain text shows a preview that says so, and asks for
`MBox_Open` or `MBox_Export` instead. A message that is malformed shows a
preview that says so, and the other messages of the mailbox stay usable.

## The dialects of a mailbox

The feature reads the `mboxrd` dialect, which is the default, and the `mboxo`
dialect. The dialect is a setting of the feature, and it is read from the
`mbox.conf` file of the configuration of the application:

```conf
format = mboxo
```

The `mboxrd` dialect removes exactly one `>` from a body line that matches
`^>+From `. The `mboxo` dialect removes one `>` only from a body line that
matches exactly `>From `, and leaves `>>From ` as it is. A mailbox that was
written in the `mboxo` dialect therefore keeps a body line that began with
`>From ` in the original message, and no reader can tell the two apart.

The `mboxcl` and `mboxcl2` dialects are not supported, even when a message of
the mailbox looks like a message of one of them.

The format of a mailbox is never guessed. A mailbox is read as the dialect that
the configuration names, and the setting applies when the mailbox is read again.

## The temporary files

`MBox_Open` writes the messages that it opens to a directory of its own, below
the temporary directory of the application:

```text
%TEMP%\Brows\MBox\{session}\message-{offset}.eml
```

The name of a file comes from the offset of the message in the mailbox, and
never from its subject, its sender, or its message ID. The files stay there
until the application exits, because the viewer that they are opened with may
read them later. Session names are unique across application processes, and each
active session holds an open ownership marker. Application startup removes
unowned directories older than seven days, including markers left by a crashed
process. Application exit releases ownership and removes its own temporary files.
A file that is locked stays until a later cleanup.

`MBox_Export` builds each file in a temporary file of the system that is deleted
when the panel has received the file, and it does not use the temporary directory
of the feature.
