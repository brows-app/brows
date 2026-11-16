using Brows.Entries;
using System;
using System.Globalization;

namespace Brows.Data;

/// <summary>
/// Converts the date of a message to the text that is shown in a column.
/// </summary>
/// <remarks>
/// The date is shown in UTC, so that the same message shows the same text on every machine. A
/// message that has no date shows nothing at all.
/// </remarks>
internal sealed class MBoxDateConverter : EntryDataConverter {
    /// <summary>
    /// Gets the format of the date.
    /// </summary>
    public string Format {
        get => field ??= "yyyy-MM-dd HH:mm:ss 'UTC'";
        set;
    }

    /// <summary>
    /// Converts the specified value to the text of a column.
    /// </summary>
    /// <param name="value">
    /// The value to convert. May be <see langword="null"/>.
    /// </param>
    /// <param name="parameter">
    /// The parameter of the conversion. May be <see langword="null"/>.
    /// </param>
    /// <param name="culture">
    /// The culture of the conversion.
    /// </param>
    /// <returns>
    /// The text of the column, or <see langword="null"/> when the value is <see langword="null"/>.
    /// </returns>
    public override string Convert(object value, object parameter, CultureInfo culture) {
        if (value is null) {
            return null;
        }
        if (value is not DateTimeOffset date) {
            if (Try<DateTimeOffset>(v => (DateTimeOffset)v, value, out var converted) == false) {
                return null;
            }
            date = converted;
        }
        var format = parameter?.ToString() ?? Format;
        return date.ToUniversalTime().ToString(format, culture);
    }
}
