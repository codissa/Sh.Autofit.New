using System.Globalization;
using System.Windows.Data;

namespace Sh.Autofit.StickerPrinting.Converters;

public class LanguageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string selectedLanguage && parameter is string languageCode)
        {
            return selectedLanguage == languageCode;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string languageCode)
        {
            return languageCode;
        }
        return "he"; // Default to Hebrew
    }
}
