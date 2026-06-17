using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UdpSimulator.Converters
{
    public class DirectionColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string s && s == "TX"
               ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))   // green
               : new SolidColorBrush(Color.FromRgb(0x29, 0xB6, 0xF6));  // blue
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
