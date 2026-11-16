namespace Brows.Windows.Data.ValueConverters;

internal sealed class IsNullConverter : ValueConverter {
    private protected sealed override object Convert(object value,
                                                     Type targetType,
                                                     object parameter,
                                                     CultureInfo culture) {
        return value is null || value == null || value.Equals(null);
    }
}
