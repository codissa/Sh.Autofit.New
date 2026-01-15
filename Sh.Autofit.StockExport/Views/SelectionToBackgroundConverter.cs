using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Sh.Autofit.StockExport.Views;

/// <summary>
/// Converts boolean IsSelected to a background brush
/// </summary>
public class SelectionToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return new SolidColorBrush(Color.FromRgb(220, 240, 255)); // Light blue
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
