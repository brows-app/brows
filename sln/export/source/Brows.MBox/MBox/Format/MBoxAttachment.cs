namespace Brows.MBox.Format;

/*
 * An attachment of a message and its bytes after its transfer encoding was decoded. The bytes
 * are null when the transfer encoding is not supported, and the attachment is incomplete when
 * the mailbox ends before the attachment does.
 */
internal sealed record MBoxAttachment(MBoxPart Part, byte[] Bytes, bool IsComplete);
