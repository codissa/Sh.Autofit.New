using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sh.Autofit.StickerPrinting.Helpers;

namespace Sh.Autofit.StickerPrinting.Models;

public class LabelData : INotifyPropertyChanged
{
    private string _introLine = "S.H. Car Rubber Import and Distribution";
    private string _itemKey = string.Empty;
    private string _description = string.Empty;
    private string _language = "he"; // "he" or "ar"
    private double _fontSize = 12.0;
    private string _fontFamily = "Arial";
    private int _quantity = 1;

    public string IntroLine
    {
        get => _introLine;
        set { _introLine = value; OnPropertyChanged(); }
    }

    public string ItemKey
    {
        get => _itemKey;
        set
        {
            _itemKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShouldShowDescription));
        }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public string Language
    {
        get => _language;
        set
        {
            _language = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsArabic));
            OnPropertyChanged(nameof(IsHebrew));
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set { _fontSize = value; OnPropertyChanged(); }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set { _fontFamily = value; OnPropertyChanged(); }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = value > 0 ? value : 1; OnPropertyChanged(); }
    }

    public bool ShouldShowDescription => !PrefixChecker.HasExcludedPrefix(ItemKey);
    public bool IsArabic => Language == "ar";
    public bool IsHebrew => Language == "he";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
