using Brows.Entries;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data;

/// <summary>
/// Holds the columns of the list of a mailbox.
/// </summary>
internal static class MBoxEntryData {
    /// <summary>
    /// Holds the definition of a column of the list of a mailbox.
    /// </summary>
    public abstract class MBoxEntryDataDefinition<T> : EntryDataExport<MBoxEntry, T> {
    }

    /// <summary>
    /// Holds the subject of a message.
    /// </summary>
    public sealed class Subject : MBoxEntryDataDefinition<string> {
        protected sealed override Task<string> GetValue(MBoxEntry entry,
                                                        Action<string> progress,
                                                        CancellationToken cancellationToken) {
            return Task.FromResult(entry.Subject);
        }

        public Subject() {
            Width = 400;
        }
    }

    /// <summary>
    /// Holds the sender of a message.
    /// </summary>
    public sealed class From : MBoxEntryDataDefinition<string> {
        protected sealed override Task<string> GetValue(MBoxEntry entry,
                                                        Action<string> progress,
                                                        CancellationToken cancellationToken) {
            return Task.FromResult(entry.From);
        }

        public From() {
            Width = 250;
        }
    }

    /// <summary>
    /// Holds the date of a message.
    /// </summary>
    public sealed class Date : MBoxEntryDataDefinition<DateTimeOffset?> {
        protected sealed override Task<DateTimeOffset?> GetValue(MBoxEntry entry,
                                                                  Action<DateTimeOffset?> progress,
                                                                  CancellationToken cancellationToken) {
            return Task.FromResult(entry.Date);
        }

        public Date() {
            Width = 200;
            Converter = new MBoxDateConverter();
        }
    }

    /// <summary>
    /// Holds the recipients of a message.
    /// </summary>
    public sealed class To : MBoxEntryDataDefinition<string> {
        protected sealed override Task<string> GetValue(MBoxEntry entry,
                                                        Action<string> progress,
                                                        CancellationToken cancellationToken) {
            return Task.FromResult(entry.To);
        }

        public To() {
            Width = 250;
        }
    }

    /// <summary>
    /// Holds the message identifier of a message.
    /// </summary>
    public sealed class MessageId : MBoxEntryDataDefinition<string> {
        protected sealed override Task<string> GetValue(MBoxEntry entry,
                                                        Action<string> progress,
                                                        CancellationToken cancellationToken) {
            return Task.FromResult(entry.MessageId);
        }
    }

    /// <summary>
    /// Holds the labels of a message.
    /// </summary>
    public sealed class Labels : MBoxEntryDataDefinition<string> {
        protected sealed override Task<string> GetValue(MBoxEntry entry,
                                                        Action<string> progress,
                                                        CancellationToken cancellationToken) {
            return Task.FromResult(entry.Labels);
        }
    }

    /// <summary>
    /// Holds the thread identifier of a message.
    /// </summary>
    public sealed class ThreadId : MBoxEntryDataDefinition<string> {
        protected sealed override Task<string> GetValue(MBoxEntry entry,
                                                        Action<string> progress,
                                                        CancellationToken cancellationToken) {
            return Task.FromResult(entry.ThreadId);
        }
    }

    /// <summary>
    /// Holds the number of bytes that the stored record of a message occupies.
    /// </summary>
    public sealed class Size : MBoxEntryDataDefinition<long?> {
        protected sealed override Task<long?> GetValue(MBoxEntry entry,
                                                        Action<long?> progress,
                                                        CancellationToken cancellationToken) {
            return Task.FromResult<long?>(entry.Size);
        }

        public Size() {
            Width = 100;
            Alignment = EntryDataAlignment.Right;
            Converter = EntryDataConverter.FileSystemSize;
        }
    }
}
