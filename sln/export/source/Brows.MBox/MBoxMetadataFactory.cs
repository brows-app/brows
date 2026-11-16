using Brows.MBox.Format;
using System;

namespace Brows;

/// <summary>
/// Creates the metadata of a message, from its header fields.
/// </summary>
internal static class MBoxMetadataFactory {
    /// <summary>
    /// Gets the maximum number of UTF-16 code units of a stored text field.
    /// </summary>
    public const int MaxTextLength = 4096;

    /// <summary>
    /// Creates the metadata of a message.
    /// </summary>
    /// <param name="message">
    /// The message to read.
    /// </param>
    /// <param name="record">
    /// The record that locates the message in the mailbox.
    /// </param>
    /// <param name="token">
    /// The token used to cancel the read.
    /// </param>
    /// <returns>
    /// The metadata of the message.
    /// </returns>
    public static MBoxMetadata Create(MBoxMessage message, MBoxRecord record, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(record);
        var headers = message.Headers;
        var date = MBoxMetadataFactory.Date(headers, record);
        return new MBoxMetadata {
            Subject = Limit(Value(headers, "Subject")),
            From = Limit(Address(headers, "From")),
            To = Limit(Address(headers, "To")),
            Date = date,
            MessageId = Limit(Value(headers, "Message-ID")),
            Labels = Limit(Value(headers, "X-Gmail-Labels")),
            ThreadId = Limit(ThreadId(headers)),
            Size = record.Length,
        };
    }

    /// <summary>
    /// Returns the number of bytes that the metadata of a message occupies.
    /// </summary>
    /// <param name="metadata">
    /// The metadata to measure.
    /// </param>
    /// <returns>
    /// The number of bytes that the metadata occupies.
    /// </returns>
    public static long Size(MBoxMetadata metadata) {
        if (metadata is null) {
            return 0;
        }
        long size = 0;
        size += Length(metadata.Subject);
        size += Length(metadata.From);
        size += Length(metadata.To);
        size += Length(metadata.MessageId);
        size += Length(metadata.Labels);
        size += Length(metadata.ThreadId);
        return size;
    }

    private static long Length(string value) {
        return value is null ? 0 : value.Length * 2L;
    }

    private static string Value(MBoxHeaders headers, string name) {
        return headers.Get(name)?.Value;
    }

    private static string Address(MBoxHeaders headers, string name) {
        return headers.Get(name)?.Value;
    }

    private static string ThreadId(MBoxHeaders headers) {
        var threadId = Value(headers, "X-GM-THRID");
        if (string.IsNullOrWhiteSpace(threadId)) {
            threadId = Value(headers, "X-Gmail-Thread-Id");
        }
        return threadId;
    }

    private static DateTimeOffset? Date(MBoxHeaders headers, MBoxRecord record) {
        /*
         * The date of the header field is preferred, and the date of the envelope is used when
         * the header field holds no valid date.
         */
        var date = headers.Date;
        if (date.HasValue) {
            return date;
        }
        return record.Envelope?.Date;
    }

    private static string Limit(string value) {
        if (value is null) {
            return null;
        }
        if (value.Length <= MaxTextLength) {
            return value;
        }
        var end = MaxTextLength;
        if (char.IsHighSurrogate(value[end - 1])) {
            end--;
        }
        return value[..end];
    }
}
