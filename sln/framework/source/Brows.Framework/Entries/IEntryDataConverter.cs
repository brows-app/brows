namespace Brows.Entries;

/// <summary>
/// Formats an entry data value as display text.
/// </summary>
public interface IEntryDataConverter {
    /// <summary>
    /// Formats a data value for display.
    /// </summary>
    /// <param name="value">
    /// The data value to format.
    /// </param>
    /// <param name="parameter">
    /// An optional converter-specific setting.
    /// </param>
    /// <param name="culture">
    /// The culture used to format the value.
    /// </param>
    /// <returns>
    /// The display text, or null when the value cannot be displayed.
    /// </returns>
    string Convert(object value, object parameter, CultureInfo culture);
}
