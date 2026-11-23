using Brows.Localization;
using System;
using CONVERT = System.Convert;

namespace Brows.Entries;

/// <summary>
/// Provides display formatting for entry data and commonly used converters.
/// </summary>
public abstract class EntryDataConverter : IEntryDataConverter {
    /// <summary>
    /// Attempts a conversion without passing conversion errors to the caller.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the converted value.
    /// </typeparam>
    /// <param name="conversion">
    /// The conversion to attempt.
    /// </param>
    /// <param name="input">
    /// The value to convert.
    /// </param>
    /// <param name="result">
    /// Receives the converted value, or the default value if conversion fails.
    /// </param>
    /// <returns>
    /// True if conversion succeeded; otherwise, false.
    /// </returns>
    protected static bool Try<T>(Func<object, T> conversion, object input, out T result) {
        try {
            result = conversion(input);
            return true;
        }
        catch {
            result = default;
            return false;
        }
    }

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
    public abstract string Convert(object value, object parameter, CultureInfo culture);

    /// <summary>
    /// Gets a date and time converter that accepts a format string as its parameter.
    /// </summary>
    public static EntryDataConverter DateTime { get; } = new DateTimeConverter();

    /// <summary>
    /// Creates a Boolean converter that displays translated Yes or No text.
    /// </summary>
    /// <param name="translation">
    /// The translation that supplies the Yes and No text.
    /// </param>
    /// <returns>
    /// The Boolean converter.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="translation"/> is null.
    /// </exception>
    public static EntryDataConverter BooleanYesNo(ITranslation translation) {
        return new BooleanYesNoConverter(translation);
    }

    /// <summary>
    /// Gets a byte-count converter that displays a readable size and accepts a number format parameter.
    /// </summary>
    public static EntryDataConverter FileSystemSize { get; } = new FileSystemSizeConverter();

    private sealed class DateTimeConverter : EntryDataConverter {
        public string Format { get; set; } = "yyyy-MM-dd HH:mm:ss";

        public sealed override string Convert(object value, object parameter, CultureInfo culture) {
            if (value == null) return null;

            var converted = Try(CONVERT.ToDateTime, value, out var dateTime);
            if (converted == false) return null;

            var format = parameter?.ToString() ?? Format;
            var formatted = dateTime.ToString(format, culture);
            return formatted;
        }
    }

    private sealed class BooleanYesNoConverter : EntryDataConverter {
        private readonly ITranslation Translation;

        public BooleanYesNoConverter(ITranslation translation) {
            Translation = translation ?? throw new ArgumentNullException(nameof(translation));
        }

        public sealed override string Convert(object value, object parameter, CultureInfo culture) {
            if (value == null) return null;

            var converted = Try(CONVERT.ToBoolean, value, out var boolean);
            if (converted == false) return null;

            var yesNo = boolean ? "Yes" : "No";
            var key = $"EntryData_{yesNo}";
            var s = Translation.Value(key);
            return s;
        }
    }

    private sealed class FileSystemSizeConverter : EntryDataConverter {
        public sealed override string Convert(object value, object parameter, CultureInfo culture) {
            if (value == null) return null;

            var converted = Try(CONVERT.ToInt64, value, out var length);
            if (converted == false) return null;

            var format = parameter?.ToString();
            var readable = FileSize.From(length, format, culture);
            return readable;
        }
    }

    internal static class FileSize {
        public static string From(long fileLength, string format, IFormatProvider formatProvider) {
            string frmt = string.IsNullOrWhiteSpace(format) ? null : format;
            string unit;
            double size;
            var abs = (fileLength < 0 ? -fileLength : fileLength);
            switch (abs) {
                case >= 0x1000000000000000:
                    unit = "EiB";
                    size = (fileLength >> 50);
                    frmt = frmt ?? "0.##";
                    break;
                case >= 0x4000000000000:
                    unit = "PiB";
                    size = (fileLength >> 40);
                    frmt = frmt ?? "0.##";
                    break;
                case >= 0x10000000000:
                    unit = "TiB";
                    size = (fileLength >> 30);
                    frmt = frmt ?? "0.##";
                    break;
                case >= 0x40000000:
                    unit = "GiB";
                    size = (fileLength >> 20);
                    frmt = frmt ?? "0.##";
                    break;
                case >= 0x100000:
                    unit = "MiB";
                    size = (fileLength >> 10);
                    frmt = frmt ?? "0.##";
                    break;
                case >= 0x400:
                    unit = "KiB";
                    size = fileLength;
                    frmt = frmt ?? "0.#";
                    break;
                default:
                    return fileLength.ToString("0 B");
            }
            size = (size / 1024);
            return size.ToString(frmt, formatProvider) + " " + unit;
        }
    }
}
