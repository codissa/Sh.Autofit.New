using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Sh.Autofit.StickerPrinting.Models;

/// <summary>
/// Wrapper model for items in the Stock Move print list.
/// Combines LabelData with UI-specific properties for the scrollable list.
/// </summary>
public class StockMoveLabelItem : INotifyPropertyChanged
{
    private LabelData _labelData;
    private BitmapSource? _previewImage;
    private bool _isLoadingPreview;
    private string _originalArabicDescription = string.Empty;

    public StockMoveLabelItem(LabelData labelData)
    {
        _labelData = labelData ?? throw new ArgumentNullException(nameof(labelData));

        // Subscribe to LabelData changes to refresh preview
        _labelData.PropertyChanged += OnLabelDataPropertyChanged;
    }

    /// <summary>
    /// The underlying label data for printing
    /// </summary>
    public LabelData LabelData
    {
        get => _labelData;
        set
        {
            if (_labelData != null)
                _labelData.PropertyChanged -= OnLabelDataPropertyChanged;

            _labelData = value;

            if (_labelData != null)
                _labelData.PropertyChanged += OnLabelDataPropertyChanged;

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Cached preview image for display in the list
    /// </summary>
    public BitmapSource? PreviewImage
    {
        get => _previewImage;
        set { _previewImage = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Whether preview is currently being generated
    /// </summary>
    public bool IsLoadingPreview
    {
        get => _isLoadingPreview;
        set { _isLoadingPreview = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Original Arabic description from database (for reference)
    /// </summary>
    public string OriginalArabicDescription
    {
        get => _originalArabicDescription;
        set { _originalArabicDescription = value; OnPropertyChanged(); }
    }

    // Convenience properties that delegate to LabelData
    public string ItemKey => _labelData.ItemKey;

    public string Description
    {
        get => _labelData.Description;
        set => _labelData.Description = value;
    }

    public string Language
    {
        get => _labelData.Language;
        set => _labelData.Language = value;
    }

    public int Quantity
    {
        get => _labelData.Quantity;
        set => _labelData.Quantity = value;
    }

    public bool IsArabic => _labelData.IsArabic;
    public bool IsHebrew => _labelData.IsHebrew;

    /// <summary>
    /// Whether Arabic editing is available (Arabic mode selected)
    /// </summary>
    public bool CanEditArabic => IsArabic;

    // Event fired when preview needs regeneration
    public event EventHandler? PreviewUpdateRequested;

    private void OnLabelDataPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Notify UI of changes
        OnPropertyChanged(nameof(LabelData));

        if (e.PropertyName == nameof(LabelData.Language))
        {
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(IsArabic));
            OnPropertyChanged(nameof(IsHebrew));
            OnPropertyChanged(nameof(CanEditArabic));
        }
        else if (e.PropertyName == nameof(LabelData.Quantity))
        {
            OnPropertyChanged(nameof(Quantity));
        }
        else if (e.PropertyName == nameof(LabelData.Description))
        {
            OnPropertyChanged(nameof(Description));
        }

        // Request preview update
        PreviewUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Cleanup()
    {
        if (_labelData != null)
            _labelData.PropertyChanged -= OnLabelDataPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
