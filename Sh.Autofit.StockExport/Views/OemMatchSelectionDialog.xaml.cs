using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Sh.Autofit.StockExport.Commands;
using Sh.Autofit.StockExport.Models;

namespace Sh.Autofit.StockExport.Views;

/// <summary>
/// Interaction logic for OemMatchSelectionDialog.xaml
/// </summary>
public partial class OemMatchSelectionDialog : Window
{
    public OemMatchSelectionDialogViewModel ViewModel { get; }

    public OemMatchSelectionDialog(List<PartLookupResult> matchedParts, string oemCode)
    {
        InitializeComponent();
        ViewModel = new OemMatchSelectionDialogViewModel(matchedParts, oemCode);
        DataContext = ViewModel;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPart != null)
        {
            DialogResult = true;
            Close();
        }
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>
/// ViewModel for OemMatchSelectionDialog
/// </summary>
public class OemMatchSelectionDialogViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<SelectablePartLookupResult> _matchedParts = new();
    private readonly string _oemCode;

    public event PropertyChangedEventHandler? PropertyChanged;

    public OemMatchSelectionDialogViewModel(List<PartLookupResult> matchedParts, string oemCode)
    {
        _oemCode = oemCode ?? string.Empty;

        foreach (var part in matchedParts)
        {
            _matchedParts.Add(new SelectablePartLookupResult(part));
        }

        SelectPartCommand = new RelayCommand(part => SelectPart((SelectablePartLookupResult)part!));
    }

    public ObservableCollection<SelectablePartLookupResult> MatchedParts => _matchedParts;

    public string OemCode => _oemCode;

    public ICommand SelectPartCommand { get; }

    public PartLookupResult? SelectedPart { get; private set; }

    private void SelectPart(SelectablePartLookupResult part)
    {
        // Deselect all
        foreach (var p in _matchedParts)
        {
            p.IsSelected = false;
        }

        // Select clicked part
        part.IsSelected = true;
        SelectedPart = part.Part;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Wrapper class to add selection state to PartLookupResult
/// </summary>
public class SelectablePartLookupResult : INotifyPropertyChanged
{
    private bool _isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public SelectablePartLookupResult(PartLookupResult part)
    {
        Part = part ?? throw new ArgumentNullException(nameof(part));
    }

    public PartLookupResult Part { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string DisplayText => Part.DisplayText;
    public string DetailsText => Part.DetailsText;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
