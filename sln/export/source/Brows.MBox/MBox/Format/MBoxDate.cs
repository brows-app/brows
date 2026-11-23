using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Brows.MBox.Format;

/// <summary>
/// Parses the date syntaxes used by mailbox envelopes and message headers.
/// </summary>
/// <remarks>
/// The parser is deliberately invariant. It never falls back to a culture-dependent parser, and it
/// never substitutes the current time or the time zone of the machine for a missing zone.
/// <para/>
/// Recognizing the shape of a date and interpreting its value are separate operations. A date
/// whose shape is recognized but whose value is impossible is reported as an invalid date, not as
/// an unrecognized one, so that it still marks a separator.
/// </remarks>
internal static class MBoxDate {
    /// <summary>
    /// The weekday names, in the order of <see cref="DayOfWeek"/>.
    /// </summary>
    private static readonly string[] Weekdays = [
        "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"
    ];

    /// <summary>
    /// The month names.
    /// </summary>
    private static readonly string[] Months = [
        "Jan", "Feb", "Mar", "Apr", "May", "Jun",
        "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
    ];

    /// <summary>
    /// The obsolete named time zones that are supported, and their offsets from UTC in minutes.
    /// </summary>
    private static readonly Dictionary<string, int> NamedZones = new(StringComparer.OrdinalIgnoreCase) {
        ["UT"] = 0,
        ["GMT"] = 0,
        ["UTC"] = 0,
        ["Z"] = 0,
        ["EST"] = -300,
        ["EDT"] = -240,
        ["CST"] = -360,
        ["CDT"] = -300,
        ["MST"] = -420,
        ["MDT"] = -360,
        ["PST"] = -480,
        ["PDT"] = -420,
    };

    /// <summary>
    /// Parses an envelope date.
    /// </summary>
    /// <param name="text">
    /// The text to parse.
    /// </param>
    /// <param name="date">
    /// The parsed date, or <see langword="null"/> when the text is not a valid date.
    /// </param>
    /// <param name="weekdayMismatch">
    /// Whether the weekday of the text disagrees with the date.
    /// </param>
    /// <param name="invalidValue">
    /// Whether the text has the expected syntax but is not a valid date.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the text has the syntax of an envelope date.
    /// </returns>
    internal static bool TryParseEnvelope(string text,
                                        out DateTimeOffset? date,
                                        out bool weekdayMismatch,
                                        out bool invalidValue) {
        date = null;
        weekdayMismatch = false;
        invalidValue = false;
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }
        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) {
            return false;
        }
        if (parts[0].EndsWith(",", StringComparison.Ordinal)) {
            /*
             * The RFC 2822 shape: "Thu, 17 Sep 2015 17:54:37 +0000".
             */
            return TryParseRfc2822(parts, out date, out weekdayMismatch, out invalidValue);
        }
        if (parts.Length == 5) {
            /*
             * The classic asctime shape: "Thu Sep 17 18:09:44 2015".
             */
            return TryParseAsctime(parts, out date, out weekdayMismatch, out invalidValue);
        }
        if (parts.Length == 6) {
            /*
             * The Google Takeout shape: "Thu Sep 17 18:09:44 +0000 2015".
             */
            return TryParseGoogle(parts, out date, out weekdayMismatch, out invalidValue);
        }
        return false;
    }

    /// <summary>
    /// Parses an RFC 5322 date header value.
    /// </summary>
    /// <param name="text">
    /// The text to parse.
    /// </param>
    /// <param name="date">
    /// The parsed date, or <see langword="null"/> when the text is not a valid date.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the text is a valid date.
    /// </returns>
    internal static bool TryParseHeader(string text, out DateTimeOffset? date) {
        return TryParseHeader(text, out date, out _);
    }

    internal static bool TryParseHeader(string text, out DateTimeOffset? date, out bool invalidValue) {
        date = null;
        invalidValue = false;
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }
        var value = RemoveComment(text.Trim());
        if (value.Length == 0) {
            return false;
        }
        var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) {
            return false;
        }
        var index = 0;
        if (parts[0].EndsWith(",", StringComparison.Ordinal)) {
            index = 1;
        }
        if (parts.Length - index < 5) {
            /*
             * The date, the time, and the time zone are all required.
             */
            return false;
        }
        if (IsNumberShape(parts[index], 1, 2) == false) {
            return false;
        }
        var month = MonthIndex(parts[index + 1]);
        if (month < 0) {
            return false;
        }
        if (IsNumberShape(parts[index + 2], 1, 4) == false) {
            return false;
        }
        if (IsTimeShape(parts[index + 3], allowMissingSeconds: true) == false) {
            return false;
        }
        var zone = parts[index + 4];
        TimeSpan offset;
        if (IsOffsetShape(zone) == false) {
            if (NamedZones.TryGetValue(zone, out var minutes) == false) {
                return false;
            }
            offset = TimeSpan.FromMinutes(minutes);
        }
        else if (TryReadOffset(zone, out offset) == false) {
            invalidValue = true;
            return false;
        }
        var day = ReadNumber(parts[index]);
        var year = ReadNumber(parts[index + 2]);
        if (TryReadTime(parts[index + 3], allowMissingSeconds: true, out var time) == false) {
            invalidValue = true;
            return false;
        }
        year = AdjustHeaderYear(year, parts[index + 2].Length);
        if (ValidDate(year, month, day) == false) {
            invalidValue = true;
            return false;
        }
        if (TryCreateDateTimeOffset(year, month, day, time, offset, out var parsed) == false) {
            invalidValue = true;
            return false;
        }
        date = parsed;
        return true;
    }

    private static bool TryParseAsctime(string[] parts,
                                       out DateTimeOffset? date,
                                       out bool weekdayMismatch,
                                       out bool invalidValue) {
        date = null;
        weekdayMismatch = false;
        invalidValue = false;
        if (WeekdayIndex(parts[0]) < 0) {
            return false;
        }
        var month = MonthIndex(parts[1]);
        if (month < 0) {
            return false;
        }
        if (IsNumberShape(parts[2], 1, 2) == false) {
            return false;
        }
        if (IsTimeShape(parts[3]) == false) {
            return false;
        }
        if (IsNumberShape(parts[4], 1, 4) == false) {
            return false;
        }
        var day = ReadNumber(parts[2]);
        var year = ReadNumber(parts[4]);
        if (TryReadTime(parts[3], out var time) == false) {
            invalidValue = true;
            return true;
        }
        weekdayMismatch = WeekdayMismatch(WeekdayIndex(parts[0]), year, month, day);
        if (ValidDate(year, month, day) == false) {
            invalidValue = true;
            return true;
        }
        /*
         * The asctime shape carries no time zone, so it is read as UTC.
         */
        if (TryCreateDateTimeOffset(year, month, day, time, TimeSpan.Zero, out var parsed) == false) {
            invalidValue = true;
            return true;
        }
        date = parsed;
        return true;
    }

    private static bool TryParseGoogle(string[] parts,
                                      out DateTimeOffset? date,
                                      out bool weekdayMismatch,
                                      out bool invalidValue) {
        date = null;
        weekdayMismatch = false;
        invalidValue = false;
        if (WeekdayIndex(parts[0]) < 0) {
            return false;
        }
        var month = MonthIndex(parts[1]);
        if (month < 0) {
            return false;
        }
        if (IsNumberShape(parts[2], 1, 2) == false) {
            return false;
        }
        if (IsTimeShape(parts[3]) == false) {
            return false;
        }
        if (IsOffsetShape(parts[4]) == false) {
            return false;
        }
        if (IsNumberShape(parts[5], 1, 4) == false) {
            return false;
        }
        var day = ReadNumber(parts[2]);
        var year = ReadNumber(parts[5]);
        var hasValidTime = TryReadTime(parts[3], out var time);
        var hasValidOffset = TryReadOffset(parts[4], out var offset);
        var timeOrOffsetIsInvalid = hasValidTime == false || hasValidOffset == false;
        if (timeOrOffsetIsInvalid) {
            invalidValue = true;
            return true;
        }
        weekdayMismatch = WeekdayMismatch(WeekdayIndex(parts[0]), year, month, day);
        if (ValidDate(year, month, day) == false) {
            invalidValue = true;
            return true;
        }
        if (TryCreateDateTimeOffset(year, month, day, time, offset, out var parsed) == false) {
            invalidValue = true;
            return true;
        }
        date = parsed;
        return true;
    }

    private static bool TryParseRfc2822(string[] parts,
                                        out DateTimeOffset? date,
                                        out bool weekdayMismatch,
                                        out bool invalidValue) {
        date = null;
        weekdayMismatch = false;
        invalidValue = false;
        if (parts.Length != 6) {
            return false;
        }
        var weekday = WeekdayIndex(parts[0][..^1]);
        if (weekday < 0) {
            return false;
        }
        if (IsNumberShape(parts[1], 1, 2) == false) {
            return false;
        }
        var month = MonthIndex(parts[2]);
        if (month < 0) {
            return false;
        }
        if (IsNumberShape(parts[3], 1, 4) == false) {
            return false;
        }
        if (IsTimeShape(parts[4]) == false) {
            return false;
        }
        if (IsOffsetShape(parts[5]) == false) {
            return false;
        }
        var day = ReadNumber(parts[1]);
        var year = ReadNumber(parts[3]);
        var hasValidTime = TryReadTime(parts[4], out var time);
        var hasValidOffset = TryReadOffset(parts[5], out var offset);
        var timeOrOffsetIsInvalid = hasValidTime == false || hasValidOffset == false;
        if (timeOrOffsetIsInvalid) {
            invalidValue = true;
            return true;
        }
        weekdayMismatch = WeekdayMismatch(weekday, year, month, day);
        if (ValidDate(year, month, day) == false) {
            invalidValue = true;
            return true;
        }
        if (TryCreateDateTimeOffset(year, month, day, time, offset, out var parsed) == false) {
            invalidValue = true;
            return true;
        }
        date = parsed;
        return true;
    }

    /// <summary>
    /// Removes the comments of a date header value.
    /// </summary>
    /// <param name="value">
    /// The value to clean.
    /// </param>
    /// <returns>
    /// The value without its comments.
    /// </returns>
    private static string RemoveComment(string value) {
        var builder = new StringBuilder(value.Length);
        var depth = 0;
        for (var i = 0; i < value.Length; i++) {
            var character = value[i];
            if (character == '(') {
                depth++;
                continue;
            }
            if (character == ')') {
                if (depth > 0) {
                    depth--;
                }
                continue;
            }
            if (depth == 0) {
                builder.Append(character);
            }
        }
        return builder.ToString().Trim();
    }

    /// <summary>
    /// Returns whether the specified text has the shape of a number of the specified width.
    /// </summary>
    /// <param name="text">
    /// The text to examine.
    /// </param>
    /// <param name="minLength">
    /// The minimum number of digits.
    /// </param>
    /// <param name="maxLength">
    /// The maximum number of digits.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified text has the shape of a number.
    /// </returns>
    private static bool IsNumberShape(string text, int minLength, int maxLength) {
        if (text is null) {
            return false;
        }
        if (text.Length < minLength || text.Length > maxLength) {
            return false;
        }
        for (var i = 0; i < text.Length; i++) {
            if (text[i] < '0' || text[i] > '9') {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns whether the specified text has the shape of a time of day.
    /// </summary>
    /// <param name="text">
    /// The text to examine.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified text has the shape of a time of day.
    /// </returns>
    private static bool IsTimeShape(string text, bool allowMissingSeconds = false) {
        if (text is null) {
            return false;
        }
        var parts = text.Split(':');
        var hasSeconds = parts.Length == 3;
        var hasOptionalSecondsShape = allowMissingSeconds && parts.Length == 2;
        var hasRequiredTimeParts = hasSeconds || hasOptionalSecondsShape;
        if (hasRequiredTimeParts == false) {
            return false;
        }
        for (var i = 0; i < parts.Length; i++) {
            if (IsNumberShape(parts[i], 1, 2) == false) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns whether the specified text has the shape of a numeric time zone offset.
    /// </summary>
    /// <param name="text">
    /// The text to examine.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the specified text has the shape of a numeric offset.
    /// </returns>
    private static bool IsOffsetShape(string text) {
        if (text is null || text.Length != 5) {
            return false;
        }
        var sign = text[0];
        if (sign != '+' && sign != '-') {
            return false;
        }
        return IsNumberShape(text.Substring(1, 2), 2, 2) && IsNumberShape(text.Substring(3, 2), 2, 2);
    }

    /// <summary>
    /// Returns the number that the specified text holds.
    /// </summary>
    /// <param name="text">
    /// The text to read.
    /// </param>
    /// <returns>
    /// The number, or zero when the text does not hold one.
    /// </returns>
    private static int ReadNumber(string text) {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    /// <summary>
    /// Reads the time of day that the specified text holds.
    /// </summary>
    /// <param name="text">
    /// The text to read.
    /// </param>
    /// <param name="time">
    /// The time of day.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the text holds a time of day that is in range.
    /// </returns>
    private static bool TryReadTime(string text, out TimeSpan time) {
        return TryReadTime(text, allowMissingSeconds: false, out time);
    }

    private static bool TryReadTime(string text, bool allowMissingSeconds, out TimeSpan time) {
        time = TimeSpan.Zero;
        var parts = text.Split(':');
        var hours = ReadNumber(parts[0]);
        var minutes = ReadNumber(parts[1]);
        var seconds = parts.Length == 3 ? ReadNumber(parts[2]) : 0;
        var hasSeconds = parts.Length == 3;
        var hasOptionalSecondsShape = allowMissingSeconds && parts.Length == 2;
        var hasValidShape = hasSeconds || hasOptionalSecondsShape;
        var timeIsOutOfRange = hours > 23 || minutes > 59 || seconds > 59;
        var timeIsInvalid = hasValidShape == false || timeIsOutOfRange;
        if (timeIsInvalid) {
            return false;
        }
        time = new TimeSpan(hours, minutes, seconds);
        return true;
    }

    /// <summary>
    /// Reads the time zone offset that the specified text holds.
    /// </summary>
    /// <param name="text">
    /// The text to read.
    /// </param>
    /// <param name="offset">
    /// The offset from UTC.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the text holds an offset that is in range.
    /// </returns>
    private static bool TryReadOffset(string text, out TimeSpan offset) {
        offset = TimeSpan.Zero;
        var hours = ReadNumber(text.Substring(1, 2));
        var minutes = ReadNumber(text.Substring(3, 2));
        var hourIsTooLarge = hours > 14;
        var minuteIsTooLarge = minutes > 59;
        var fourteenHourOffsetHasExtraMinutes = hours == 14 && minutes != 0;
        var offsetIsOutOfRange = hourIsTooLarge || minuteIsTooLarge || fourteenHourOffsetHasExtraMinutes;
        if (offsetIsOutOfRange) {
            return false;
        }
        var total = hours * 60 + minutes;
        offset = TimeSpan.FromMinutes(text[0] == '-' ? -total : total);
        return true;
    }

    private static int AdjustHeaderYear(int year, int digits) {
        if (digits <= 2) {
            return year < 50 ? year + 2000 : year + 1900;
        }
        if (digits == 3) {
            return year + 1900;
        }
        return year;
    }

    private static bool TryCreateDateTimeOffset(int year,
                                                int month,
                                                int day,
                                                TimeSpan time,
                                                TimeSpan offset,
                                                out DateTimeOffset date) {
        date = default;
        var offsetIsRepresentable = offset.Ticks % TimeSpan.TicksPerMinute == 0 &&
                                    offset.Duration() <= TimeSpan.FromHours(14);
        if (offsetIsRepresentable == false) {
            return false;
        }
        var local = new DateTime(
            year,
            month + 1,
            day,
            time.Hours,
            time.Minutes,
            time.Seconds,
            DateTimeKind.Unspecified);
        var utcTicks = local.Ticks - offset.Ticks;
        var utcTicksAreOutOfRange = utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks;
        if (utcTicksAreOutOfRange) {
            return false;
        }
        date = new DateTimeOffset(local, offset);
        return true;
    }

    private static int WeekdayIndex(string text) {
        for (var i = 0; i < Weekdays.Length; i++) {
            if (text.Equals(Weekdays[i], StringComparison.OrdinalIgnoreCase)) {
                return i;
            }
        }
        return -1;
    }

    private static int MonthIndex(string text) {
        for (var i = 0; i < Months.Length; i++) {
            if (text.Equals(Months[i], StringComparison.OrdinalIgnoreCase)) {
                return i;
            }
        }
        return -1;
    }

    private static bool ValidDate(int year, int month, int day) {
        if (year < 1 || year > 9999) {
            return false;
        }
        if (month < 0 || month > 11) {
            return false;
        }
        if (day < 1 || day > DateTime.DaysInMonth(year, month + 1)) {
            return false;
        }
        return true;
    }

    private static bool WeekdayMismatch(int weekday, int year, int month, int day) {
        if (weekday < 0 || year < 1 || year > 9999 || month < 0 || month > 11) {
            return false;
        }
        if (day < 1 || day > DateTime.DaysInMonth(year, month + 1)) {
            return false;
        }
        var actual = (int)new DateTime(year, month + 1, day).DayOfWeek;
        return actual != weekday;
    }
}
