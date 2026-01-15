using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sh.Autofit.StockExport.Views;

/// <summary>
/// Converts a string to Visibility: Visible if not empty, Collapsed if empty
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
