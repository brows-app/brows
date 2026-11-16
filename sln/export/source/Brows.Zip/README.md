# ZIP archives

The ZIP provider browses `.zip` archives, including their files and empty
directories. Provider IDs use explicit fields so archive files and directories
with the same name remain distinct:

```text
zip:1|f=C:\archives\files.zip|d=
zip:1|f=C:\archives\files.zip|d=docs
zip:1|f=C:\archives\files.zip|d=folder.zip
zip:1|f=C:\archives\files.zip|n=folder/inner.zip|d=docs
```

`f` is the outer filesystem archive, each `n` is a nested archive member, and
`d` is a directory inside the innermost archive. Empty `d` identifies the
archive root. Field values use UTF-8 percent escapes for reserved characters;
for example, a directory named `a>b` is written as `d=a%3Eb`. The explicit
`n` field identifies nested archives, so a directory named `folder.zip` stays
a directory.

The former `file.zip>directory` composite IDs are no longer supported. Replace
old composite ZIP IDs saved in bookmarks, history, or configuration with the new format.
The filesystem ZIP command creates archives and extracts them to disk. Provider
commands support opening, copying, deleting, clipboard transfer, and previewing
archive entries.
