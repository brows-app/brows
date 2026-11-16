namespace Brows.MBox.Format;

/// <summary>
/// Describes a single header field of a message.
/// </summary>
public sealed record MBoxHeader {
    /// <summary>
    /// Gets the name of the header field.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the raw value of the header field, unfolded and with outer whitespace removed.
    /// </summary>
    public string RawValue { get; }

    /// <summary>
    /// Gets the value of the header field, decoded for display.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the zero-based index of the field among the fields of its header block.
    /// </summary>
    public int RawIndex { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MBoxHeader"/> class.
    /// </summary>
    /// <param name="name">
    /// The name of the header field.
    /// </param>
    /// <param name="rawValue">
    /// The raw value of the header field.
    /// </param>
    /// <param name="value">
    /// The value of the header field, decoded for display.
    /// </param>
    /// <param name="rawIndex">
    /// The zero-based index of the field among the fields of its header block.
    /// </param>
    public MBoxHeader(string name, string rawValue, string value, int rawIndex) {
        Name = name ?? string.Empty;
        RawValue = rawValue ?? string.Empty;
        Value = value ?? string.Empty;
        RawIndex = rawIndex;
    }
}
