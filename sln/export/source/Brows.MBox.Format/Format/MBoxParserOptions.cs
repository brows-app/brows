using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.MBox.Format;

/// <summary>
/// Immutable, validated options that control how a mailbox is read.
/// </summary>
public sealed record MBoxParserOptions {
    private const int BufferSizeLimit = 64 * 1024 * 1024;
    private const int EnvelopeBytesLimit = 16 * 1024 * 1024;
    private const int HeaderBytesLimit = 256 * 1024 * 1024;
    private const int HeaderCountLimit = 1024 * 1024;
    private const int MimeDepthLimit = 256;
    private const int PartCountLimit = 1024 * 1024;
    private const int PreviewInputBytesLimit = 1024 * 1024 * 1024;
    private const int DecodedTextBytesLimit = 1024 * 1024 * 1024;
    private const int PreviewCharactersLimit = 64 * 1024 * 1024;

    /// <summary>
    /// Gets the mailbox storage dialect used when reading a mailbox.
    /// </summary>
    public MBoxFormat Format {
        get;
        init => field = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(
            nameof(Format),
            value,
            "The mailbox format is not a defined value.");
    } = MBoxFormat.Mboxrd;

    /// <summary>
    /// Gets the size, in bytes, of the buffer used to read the mailbox.
    /// </summary>
    public int BufferSize {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(nameof(BufferSize), value, "The buffer size must be positive.");
            }
            if (value > BufferSizeLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(BufferSize),
                    value,
                    $"The buffer size must not exceed {BufferSizeLimit} bytes.");
            }
            field = value;
        }
    } = 256 * 1024;

    /// <summary>
    /// Gets the maximum number of bytes read while looking for an envelope line.
    /// </summary>
    public int MaxEnvelopeBytes {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxEnvelopeBytes),
                    value,
                    "The maximum envelope size must be positive.");
            }
            if (value > EnvelopeBytesLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxEnvelopeBytes),
                    value,
                    $"The maximum envelope size must not exceed {EnvelopeBytesLimit} bytes.");
            }
            field = value;
        }
    } = 4 * 1024;

    /// <summary>
    /// Gets the maximum number of raw bytes read for a single header block.
    /// </summary>
    public int MaxHeaderBytes {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxHeaderBytes),
                    value,
                    "The maximum header size must be positive.");
            }
            if (value > HeaderBytesLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxHeaderBytes),
                    value,
                    $"The maximum header size must not exceed {HeaderBytesLimit} bytes.");
            }
            field = value;
        }
    } = 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of header fields read for a single header block.
    /// </summary>
    public int MaxHeaderCount {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxHeaderCount),
                    value,
                    "The maximum header count must be positive.");
            }
            if (value > HeaderCountLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxHeaderCount),
                    value,
                    $"The maximum header count must not exceed {HeaderCountLimit}.");
            }
            field = value;
        }
    } = 4096;

    /// <summary>
    /// Gets the maximum depth of a MIME part tree.
    /// </summary>
    public int MaxMimeDepth {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxMimeDepth),
                    value,
                    "The maximum MIME depth must be positive.");
            }
            if (value > MimeDepthLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxMimeDepth),
                    value,
                    $"The maximum MIME depth must not exceed {MimeDepthLimit}.");
            }
            field = value;
        }
    } = 16;

    /// <summary>
    /// Gets the maximum number of MIME parts visited for a single message.
    /// </summary>
    public int MaxPartCount {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxPartCount),
                    value,
                    "The maximum part count must be positive.");
            }
            if (value > PartCountLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxPartCount),
                    value,
                    $"The maximum part count must not exceed {PartCountLimit}.");
            }
            field = value;
        }
    } = 1024;

    /// <summary>
    /// Gets the maximum number of stored body bytes inspected for a single preview.
    /// </summary>
    public int MaxPreviewInputBytes {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxPreviewInputBytes),
                    value,
                    "The maximum preview input size must be positive.");
            }
            if (value > PreviewInputBytesLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxPreviewInputBytes),
                    value,
                    $"The maximum preview input size must not exceed {PreviewInputBytesLimit} bytes.");
            }
            field = value;
        }
    } = 8 * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of decoded text bytes retained for a single preview.
    /// </summary>
    public int MaxDecodedTextBytes {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxDecodedTextBytes),
                    value,
                    "The maximum decoded text size must be positive.");
            }
            if (value > DecodedTextBytesLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxDecodedTextBytes),
                    value,
                    $"The maximum decoded text size must not exceed {DecodedTextBytesLimit} bytes.");
            }
            field = value;
        }
    } = 8 * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of UTF-16 code units retained for a single preview.
    /// </summary>
    public int MaxPreviewCharacters {
        get;
        init {
            if (value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxPreviewCharacters),
                    value,
                    "The maximum preview character count must be positive.");
            }
            if (value > PreviewCharactersLimit) {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxPreviewCharacters),
                    value,
                    $"The maximum preview character count must not exceed {PreviewCharactersLimit}.");
            }
            field = value;
        }
    } = 1024 * 1024;

    /// <summary>
    /// Gets the default options, which read a mailbox using the mboxrd dialect.
    /// </summary>
    public static MBoxParserOptions Default {
        get => field ??= new();
    }

    /// <summary>
    /// Returns a copy of these options with the specified format applied.
    /// </summary>
    /// <param name="format">
    /// The mailbox storage dialect to use.
    /// </param>
    /// <returns>
    /// A new set of options using <paramref name="format"/>.
    /// </returns>
    public MBoxParserOptions WithFormat(MBoxFormat format) {
        return this with { Format = format };
    }
}
