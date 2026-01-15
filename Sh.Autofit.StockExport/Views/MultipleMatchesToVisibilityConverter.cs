using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Views;

/// <summary>
/// Converts ValidationStatus to Visibility: Visible if MultipleOemMatches, Collapsed otherwise
/// </summary>
public class MultipleMatchesToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationStatus status && status == ValidationStatus.MultipleOemMatches)
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
