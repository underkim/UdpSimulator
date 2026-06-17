using System.Globalization;
using System.Windows.Data;

namespace UdpSimulator.Converters
{
    public class BoolNegateConverter : IValueConverter
    {
        public static readonly BoolNegateConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
    }
}
