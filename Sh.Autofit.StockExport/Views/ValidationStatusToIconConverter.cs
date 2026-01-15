using System;
using System.Globalization;
using System.Windows.Data;
using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Views;

/// <summary>
/// Converts ValidationStatus enum to a visual icon
/// </summary>
public class ValidationStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ValidationStatus status)
        {
            return status switch
            {
                ValidationStatus.Valid => "✓",
                ValidationStatus.MultipleOemMatches => "⚠",
                _ => "✗"
            };
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
