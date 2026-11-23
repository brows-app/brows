namespace Brows;

/*
 * An attachment of a message, as the preview lists it. The name is the file name of the
 * attachment, or a text that says it has none.
 */
internal sealed record MBoxPreviewAttachment(string Name, string ContentType);
