using Brows.Windows.Data.MultiValueConverters;
using Brows.Windows.Data.ValueConverters;
using System;
using System.Windows;
using System.Windows.Data;

namespace Brows.Windows.Data;

/// <summary>
/// Base class for types that implement <see cref="IValueConverter"/>.
/// </summary>
public abstract class ValueConverter : IValueConverter {
    private protected ValueConverter() {
    }

    private protected virtual object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }

    private protected virtual object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }

    /// <summary>
    /// Creates an <see cref="IValueConverter"/> that delegates conversion to the specified functions.
    /// </summary>
    /// <param name="onConvert">
    /// The function that converts a value from the binding source to the target,
    /// or null if forward conversion is not supported.
    /// </param>
    /// <param name="onConvertBack">
    /// The function that converts a value from the binding target to the source,
    /// or null if backward conversion is not supported.
    /// </param>
    /// <returns>An <see cref="IValueConverter"/> backed by the specified conversion functions.</returns>
    public static IValueConverter Delegate(
        Func<object, Type, object, CultureInfo, object> onConvert = null,
        Func<object, Type, object, CultureInfo, object> onConvertBack = null) =>
        new DelegateConverter {
            OnConvert = onConvert,
            OnConvertBack = onConvertBack
        };

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// boolean true if the value to be converted is null, or false otherwise.
    /// </summary>
    public static IValueConverter IsNull { get; } = new IsNullConverter();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// boolean true if the value to be converted is false, or false otherwise.
    /// </summary>
    public static IValueConverter IsFalse { get; } = new IsFalseConverter();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="DependencyProperty.UnsetValue"/> if the value to be
    /// converted is null or boolean false. Otherwise, the return value
    /// is the parameter passed to <see cref="IValueConverter.Convert(object, Type, object, CultureInfo)"/>.
    /// </summary>
    public static IValueConverter Fallback { get; } = new FallbackConverter.Default();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="DependencyProperty.UnsetValue"/> if the value to be
    /// converted is null. Otherwise, the return value is the parameter passed to
    /// <see cref="IValueConverter.Convert(object, Type, object, CultureInfo)"/>.
    /// </summary>
    public static IValueConverter FallbackIfNull { get; } = new FallbackConverter.IfNull();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="DependencyProperty.UnsetValue"/> if the value to be
    /// converted is null, an empty string, or a whitespace string. Otherwise,
    /// the return value is the parameter passed to
    /// <see cref="IValueConverter.Convert(object, Type, object, CultureInfo)"/>.
    /// </summary>
    public static IValueConverter FallbackIfNullOrWhitespace { get; } =
        new FallbackConverter.IfNullOrWhitespace();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="DependencyProperty.UnsetValue"/> if the value to be
    /// converted is false. Otherwise, the return value is the parameter passed to
    /// <see cref="IValueConverter.Convert(object, Type, object, CultureInfo)"/>.
    /// </summary>
    public static IValueConverter FallbackIfFalse { get; } = new FallbackConverter.IfFalse();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="DependencyProperty.UnsetValue"/> if the value to be
    /// converted is zero. Otherwise, the return value is the parameter passed to
    /// <see cref="IValueConverter.Convert(object, Type, object, CultureInfo)"/>.
    /// </summary>
    public static IValueConverter FallbackIfZero { get; } = new FallbackConverter.IfZero();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="DependencyProperty.UnsetValue"/> for null, boolean false,
    /// a null character, an empty string, or a supported numeric zero.
    /// Otherwise, the return value is the binding converter parameter.
    /// </summary>
    public static IValueConverter FallbackIfFalsy { get; } = new FallbackConverter.Falsy();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that converts a
    /// <see cref="System.DateTime"/> to a culture-specific local date and time string.
    /// The binding converter parameter may specify a custom format string.
    /// </summary>
    public static IValueConverter DateTime { get; } = new DateTimeConverter();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="Visibility.Collapsed"/> for null, boolean false, or an empty string.
    /// Otherwise, the return value is <see cref="Visibility.Visible"/>.
    /// </summary>
    public static IValueConverter FallbackToCollapsed { get; } = new VisibilityConverter(
        setVisibility: Visibility.Visible,
        unsetVisibility: Visibility.Collapsed);

    public static IValueConverter FallbackToCollapsedWithZeroUnset { get; } = new VisibilityConverter(
        setVisibility: Visibility.Visible,
        unsetVisibility: Visibility.Collapsed,
        zeroIsUnset: true);

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="Visibility.Visible"/> for null, boolean false, or an empty string.
    /// Otherwise, the return value is <see cref="Visibility.Collapsed"/>.
    /// </summary>
    public static IValueConverter NormallyCollapsed { get; } = new VisibilityConverter(
        setVisibility: Visibility.Collapsed,
        unsetVisibility: Visibility.Visible);

    public static IValueConverter NormallyCollapsedWithZeroUnset { get; } = new VisibilityConverter(
        setVisibility: Visibility.Collapsed,
        unsetVisibility: Visibility.Visible,
        zeroIsUnset: true);

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="Visibility.Hidden"/> for null, boolean false, or an empty string.
    /// Otherwise, the return value is <see cref="Visibility.Visible"/>.
    /// </summary>
    public static IValueConverter FallbackToHidden { get; } = new VisibilityConverter(
        setVisibility: Visibility.Visible,
        unsetVisibility: Visibility.Hidden);

    public static IValueConverter FallbackToHiddenWithZeroUnset { get; } = new VisibilityConverter(
        setVisibility: Visibility.Visible,
        unsetVisibility: Visibility.Hidden,
        zeroIsUnset: true);

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that returns
    /// <see cref="Visibility.Visible"/> for null, boolean false, or an empty string.
    /// Otherwise, the return value is <see cref="Visibility.Hidden"/>.
    /// </summary>
    public static IValueConverter NormallyHidden { get; } = new VisibilityConverter(
        setVisibility: Visibility.Hidden,
        unsetVisibility: Visibility.Visible);

    public static IValueConverter NormallyHiddenWithZeroUnset { get; } = new VisibilityConverter(
        setVisibility: Visibility.Hidden,
        unsetVisibility: Visibility.Visible,
        zeroIsUnset: true);

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that converts the
    /// string representation of a value to uppercase using the current culture.
    /// </summary>
    public static IValueConverter ToUpper { get; } = new ToUpperConverter();

    /// <summary>
    /// Gets an instance of <see cref="IValueConverter"/> that converts the
    /// string representation of a value to lowercase using the current culture.
    /// </summary>
    public static IValueConverter ToLower { get; } = new ToLowerConverter();

    /// <summary>
    /// Gets an instance of <see cref="IMultiValueConverter"/> that returns the binding
    /// converter parameter when all input values are equal, or
    /// <see cref="DependencyProperty.UnsetValue"/> otherwise.
    /// </summary>
    public static IMultiValueConverter ValuesEqual { get; } = new ValuesEqualConverter();

    object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        return Convert(value, targetType, parameter, culture);
    }

    object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        return ConvertBack(value, targetType, parameter, culture);
    }
}
