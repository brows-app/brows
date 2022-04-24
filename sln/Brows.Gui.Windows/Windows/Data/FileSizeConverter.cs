using System;
using System.Globalization;

namespace Brows.Windows.Data {
    internal class FileSizeConverter : ValueConverter {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            return null; // return FileSystemSizeFormatter.From(value, parameter?.ToString());
        }
    }
}
