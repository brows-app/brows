using Brows.MBox.Format;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Brows;

/*
 * The file that exports one message of a mailbox: the message as an Internet message, or a ZIP
 * archive that holds the message and its attachments. The file is written to a temporary file
 * when the stream is made ready, and the temporary file is deleted when the stream is closed.
 */
internal sealed class MBoxExportStreamSource : EntryStreamSource<MBoxEntry> {
    private const int BufferSize = 81920;

    private sealed class Ready : EntryStreamReady {
        private readonly Stream Stream;

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                Stream.Dispose();
            }
            base.Dispose(disposing);
        }

        internal Ready(Stream stream) {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }
    }

    private bool Archive { get; }
    private long Length { get; set; } = -1;

    private static FileStream CreateTemporary() {
        var path = Path.Combine(Path.GetTempPath(), $"brows-mbox-{Guid.NewGuid():N}.tmp");
        return new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            BufferSize,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }

    private void Write(Stream destination, CancellationToken token) {
        var session = Entry.Session;
        using var lease = session.TryAcquireLease();
        if (lease is null) {
            throw new InvalidOperationException("The mailbox of the message was already retired.");
        }
        if (Archive) {
            WriteArchive(session.File, destination, token);
            return;
        }
        session.File.ExtractMessage(Entry.Record, destination, token);
    }

    private void WriteArchive(MBoxFile file, Stream destination, CancellationToken token) {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        var messageName = MBoxPath.MessageName(Entry.Record.Offset);
        var message = archive.CreateEntry(messageName, CompressionLevel.Optimal);
        using (var stream = message.Open()) {
            file.ExtractMessage(Entry.Record, stream, token);
        }
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { messageName };
        var attachmentsWereRead = file.ExtractAttachments(Entry.Record, attachment => {
            var attachmentCanBeWritten = attachment.Bytes is not null && attachment.IsComplete;
            if (attachmentCanBeWritten == false) {
                /*
                 * An attachment that the mailbox cuts short, or whose transfer encoding is not
                 * supported, is not written, so that the archive holds no damaged attachment.
                 */
                return;
            }
            token.ThrowIfCancellationRequested();
            var name = MBoxAttachmentName.Unique(attachment.Part.FileName, names);
            names.Add(name);
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(attachment.Bytes);
        }, token);
        if (attachmentsWereRead == false) {
            /*
             * A limit stopped the read, so the archive would lack attachments that the message
             * holds. The archive is not exported rather than exported without them.
             */
            throw new InvalidDataException(
                "The message holds more attachment data than the mailbox reader allows, " +
                "so its archive would be incomplete.");
        }
    }

    protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
        var temporary = CreateTemporary();
        try {
            await Task.Run(() => Write(temporary, token), token);
            await temporary.FlushAsync(token);
            temporary.Position = 0;
        }
        catch {
            await temporary.DisposeAsync();
            throw;
        }
        Length = temporary.Length;
        Stream = temporary;
        return new Ready(temporary);
    }

    internal MBoxExportStreamSource(MBoxEntry entry, bool archive) : base(entry) {
        Archive = archive;
    }

    public sealed override long StreamLength => Length;

    public sealed override string RelativePath => Archive
        ? MBoxPath.ArchiveName(Entry.Record.Offset)
        : MBoxPath.MessageName(Entry.Record.Offset);
}
