using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Sh.Autofit.StickerPrinting.Models;
using Sh.Autofit.StickerPrinting.Services.Database;

namespace Sh.Autofit.StickerPrinting.Views;

public partial class AddItemDialog : Window
{
    private readonly IPartDataService? _partDataService;
    private readonly DispatcherTimer _debounceTimer;
    private CancellationTokenSource? _searchCts;
    private bool _suppressSearch = false;
    private PartInfo? _selectedPart;

    public string ItemKey => ItemKeyTextBox.Text.Trim().ToUpperInvariant();

    public int Quantity
    {
        get => int.TryParse(QuantityTextBox.Text, out var qty) ? Math.Max(1, qty) : 1;
    }

    public AddItemDialog(IPartDataService? partDataService = null)
    {
        InitializeComponent();

        _partDataService = partDataService;

        // Initialize debounce timer
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _debounceTimer.Tick += async (s, e) =>
        {
            _debounceTimer.Stop();
            await PerformSearchAsync(ItemKeyTextBox.Text);
        };

        ItemKeyTextBox.Focus();
    }

    private void ItemKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSearch || _partDataService == null)
            return;

        _debounceTimer.Stop();

        var searchTerm = ItemKeyTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
        {
            SuggestionsList.Items.Clear();
            SuggestionsPopup.IsOpen = false;
            ClearSelectedItem();
            return;
        }

        _debounceTimer.Start();
    }

    private async Task PerformSearchAsync(string searchTerm)
    {
        if (_partDataService == null)
            return;

        // Cancel previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        try
        {
            var results = await _partDataService.SearchPartsAsync(searchTerm);

            SuggestionsList.Items.Clear();
            foreach (var part in results.Take(10))
            {
                SuggestionsList.Items.Add(part);
            }

            SuggestionsPopup.IsOpen = SuggestionsList.Items.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search failed: {ex.Message}");
        }
    }

    private void ItemKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (SuggestionsPopup.IsOpen && SuggestionsList.Items.Count > 0)
                {
                    SuggestionsList.Focus();
                    SuggestionsList.SelectedIndex = 0;
                    e.Handled = true;
                }
                break;

            case Key.Escape:
                SuggestionsPopup.IsOpen = false;
                e.Handled = true;
                break;

            case Key.Enter:
                if (SuggestionsPopup.IsOpen && SuggestionsList.Items.Count > 0)
                {
                    // Select first item if popup is open
                    SelectItem(SuggestionsList.Items[0] as PartInfo);
                    e.Handled = true;
                }
                break;
        }
    }

    private void SuggestionsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (SuggestionsList.SelectedItem is PartInfo selected)
                {
                    SelectItem(selected);
                    e.Handled = true;
                }
                break;

            case Key.Escape:
                SuggestionsPopup.IsOpen = false;
                ItemKeyTextBox.Focus();
                e.Handled = true;
                break;

            case Key.Up:
                if (SuggestionsList.SelectedIndex == 0)
                {
                    ItemKeyTextBox.Focus();
                    e.Handled = true;
                }
                break;
        }
    }

    private void SuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Preview selection (don't commit yet)
    }

    private void SuggestionsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SuggestionsList.SelectedItem is PartInfo selected)
        {
            SelectItem(selected);
        }
    }

    private void SelectItem(PartInfo? part)
    {
        if (part == null)
            return;

        _selectedPart = part;

        // Update textbox without triggering search
        _suppressSearch = true;
        ItemKeyTextBox.Text = part.ItemKey;
        ItemKeyTextBox.CaretIndex = ItemKeyTextBox.Text.Length;
        _suppressSearch = false;

        // Close popup
        SuggestionsPopup.IsOpen = false;

        // Show selected item info
        ShowSelectedItem(part);

        // Focus quantity for quick entry
        QuantityTextBox.Focus();
        QuantityTextBox.SelectAll();
    }

    private void ShowSelectedItem(PartInfo part)
    {
        SelectedItemKeyText.Text = part.ItemKey;

        // Show Hebrew or Arabic description
        var description = !string.IsNullOrWhiteSpace(part.HebrewDescription)
            ? part.HebrewDescription
            : part.PartName;

        SelectedItemDescText.Text = description;

        // Make the panel visible
        SelectedItemPanel.Tag = "Visible";
        SelectedItemPanel.Visibility = Visibility.Visible;

        // Also update the parent border visibility
        if (SelectedItemPanel.Parent is Border border)
        {
            border.Visibility = Visibility.Visible;
        }
    }

    private void ClearSelectedItem()
    {
        _selectedPart = null;
        SelectedItemKeyText.Text = string.Empty;
        SelectedItemDescText.Text = string.Empty;
        SelectedItemPanel.Tag = "Collapsed";
        SelectedItemPanel.Visibility = Visibility.Collapsed;

        if (SelectedItemPanel.Parent is Border border)
        {
            border.Visibility = Visibility.Collapsed;
        }
    }

    private void QuantityTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            // Submit the dialog
            if (!string.IsNullOrWhiteSpace(ItemKey))
            {
                DialogResult = true;
                Close();
                e.Handled = true;
            }
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ItemKey))
        {
            MessageBox.Show("נא להזין מק''ט", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
